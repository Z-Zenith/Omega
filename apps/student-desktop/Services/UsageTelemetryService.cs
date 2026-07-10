using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using StudentDesktop.Models;

namespace StudentDesktop.Services;

// SDA-25: reports usage-pattern telemetry to the Backend API (which forwards it to AI
// Services for suspicious-behaviour analysis, per AIS-07) while — and only while — a
// class session or assignment window is active. `ClassLockService.IsLocked` (SDA-01) and
// `AssignmentAutoSubmitService.IsAssignmentOpen` (SDA-11/22) are the two windows this
// keys off; outside both, every Record* call is a silent no-op, so nothing is ever queued
// or sent.
public sealed class UsageTelemetryService : IDisposable
{
    private readonly ApiClient _apiClient;
    private readonly ClassLockService _classLockService;
    private readonly AssignmentAutoSubmitService _autoSubmitService;
    private readonly Timer _flushTimer;
    private readonly List<TelemetryEventRequest> _pending = [];
    private readonly object _gate = new();
    private bool _disposed;

    public UsageTelemetryService(ApiClient apiClient, ClassLockService classLockService, AssignmentAutoSubmitService autoSubmitService, TimeSpan? flushInterval = null)
    {
        _apiClient = apiClient;
        _classLockService = classLockService;
        _autoSubmitService = autoSubmitService;
        _flushTimer = new Timer((flushInterval ?? TimeSpan.FromSeconds(30)).TotalMilliseconds) { AutoReset = true };
        _flushTimer.Elapsed += (_, _) => _ = FlushAsync();
    }

    public void Start() => _flushTimer.Start();

    public void Stop() => _flushTimer.Stop();

    public bool IsWindowActive => _classLockService.IsLocked || _autoSubmitService.IsAssignmentOpen;

    // assignmentId lets a caller tag an event with the specific assignment it belongs to;
    // if omitted, the event is assumed to belong to whatever class session is currently
    // active — the backend resolves that server-side rather than trusting a client-
    // supplied session id (see TelemetryController).
    public void Record(string eventType, Dictionary<string, object>? metadata = null, Guid? assignmentId = null)
    {
        if (!IsWindowActive)
        {
            return;
        }

        lock (_gate)
        {
            _pending.Add(new TelemetryEventRequest(eventType, metadata, assignmentId, DateTime.UtcNow));
        }
    }

    // Call periodically (e.g. from the same timer driving ClassLockService) to flush
    // whatever has accumulated since the last flush. Best-effort: a failed flush leaves
    // events queued for the next attempt rather than dropping them, but is capped to
    // avoid unbounded growth if the backend is down for a long stretch.
    private const int MaxQueueSize = 500;

    public async Task FlushAsync()
    {
        List<TelemetryEventRequest> batch;
        lock (_gate)
        {
            if (_pending.Count == 0)
            {
                return;
            }
            batch = [.. _pending];
        }

        try
        {
            await _apiClient.SubmitTelemetryAsync(batch);
            lock (_gate)
            {
                foreach (var sent in batch)
                {
                    _pending.Remove(sent);
                }
            }
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            lock (_gate)
            {
                if (_pending.Count > MaxQueueSize)
                {
                    _pending.RemoveRange(0, _pending.Count - MaxQueueSize);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _flushTimer.Dispose();
    }
}
