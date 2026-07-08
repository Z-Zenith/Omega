using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using StudentDesktop.Models;

namespace StudentDesktop.Services;

public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

// Session-scoped: the desktop app is locked-down and re-authenticates each launch (SDA-02),
// so the JWT only needs to live in memory, not persisted to disk.
public class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string? Token { get; private set; }

    public ApiClient(string baseAddress = "http://localhost:8080")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    }

    public async Task<LoginResponse> LoginAsync(string identifier, string password, string totpCode)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/auth/login",
            new LoginRequest(identifier, password, totpCode, Environment.MachineName));
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions)
            ?? throw new ApiException(500, "Empty login response");
        Token = login.Token;
        return login;
    }

    public async Task<MyCalendarResponse> GetMyCalendarAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/calendar/mine");
        return await response.Content.ReadFromJsonAsync<MyCalendarResponse>(JsonOptions)
            ?? new MyCalendarResponse([]);
    }

    public async Task<List<EventDto>> ListEventsAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/events");
        return await response.Content.ReadFromJsonAsync<List<EventDto>>(JsonOptions) ?? [];
    }

    public async Task RegisterForEventAsync(Guid eventId)
    {
        await SendAsync(HttpMethod.Post, $"/api/v1/events/{eventId}/register");
    }

    // SDA-23: self-service password change requires a fresh, successful TOTP challenge —
    // the backend rejects the request outright if the code is missing or invalid.
    public async Task ChangePasswordAsync(string currentPassword, string newPassword, string totpCode)
    {
        await SendAsync(HttpMethod.Post, "/api/v1/auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword, totpCode));
    }

    public async Task<MyMarksResponse> GetMyMarksAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/marks/mine");
        return await response.Content.ReadFromJsonAsync<MyMarksResponse>(JsonOptions)
            ?? new MyMarksResponse([], []);
    }

    // SDA-11: called by AssignmentAutoSubmitService when the app detects exit or
    // focus-loss during an active assignment window.
    public async Task<SubmissionDto> AutoSubmitAssignmentAsync(Guid assignmentId, string contentUrl, string submissionFormat)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/v1/assignments/{assignmentId}/submissions/auto-submit",
            new SubmitAssignmentRequest(contentUrl, submissionFormat));
        return await response.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions)
            ?? throw new ApiException(500, "Empty auto-submit response");
    }

    // SDA-03/SDA-04
    public async Task<WhitelistResponse> GetWhitelistAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/whitelist");
        return await response.Content.ReadFromJsonAsync<WhitelistResponse>(JsonOptions)
            ?? new WhitelistResponse([]);
    }

    // SDA-08
    public async Task<List<NoteSummaryDto>> GetMyNotesAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/notes/mine");
        return await response.Content.ReadFromJsonAsync<List<NoteSummaryDto>>(JsonOptions) ?? [];
    }

    public async Task<NoteDto> GetNoteAsync(Guid noteId)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/notes/{noteId}");
        return await response.Content.ReadFromJsonAsync<NoteDto>(JsonOptions)
            ?? throw new ApiException(500, "Empty note response");
    }

    public async Task<NoteDto> CreateNoteAsync(string title, string contentMarkdown, Guid? id = null, IReadOnlyList<NoteLinkInput>? links = null)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/notes", new CreateNoteRequest(title, contentMarkdown, id, links));
        return await response.Content.ReadFromJsonAsync<NoteDto>(JsonOptions)
            ?? throw new ApiException(500, "Empty note response");
    }

    public async Task<NoteDto> UpdateNoteAsync(Guid noteId, string title, string contentMarkdown, IReadOnlyList<NoteLinkInput>? links = null)
    {
        var response = await SendAsync(HttpMethod.Patch, $"/api/v1/notes/{noteId}", new UpdateNoteRequest(title, contentMarkdown, links));
        return await response.Content.ReadFromJsonAsync<NoteDto>(JsonOptions)
            ?? throw new ApiException(500, "Empty note response");
    }

    // SDA-19
    public async Task DeleteNoteAsync(Guid noteId)
    {
        await SendAsync(HttpMethod.Delete, $"/api/v1/notes/{noteId}");
    }

    // SDA-19/SEK-03: onListBacklinks
    public async Task<List<NoteDto>> GetBacklinksAsync(Guid noteId)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/notes/{noteId}/backlinks");
        return await response.Content.ReadFromJsonAsync<List<NoteDto>>(JsonOptions) ?? [];
    }

    public async Task LogoutAsync()
    {
        if (Token is null)
        {
            return;
        }
        try
        {
            await SendAsync(HttpMethod.Post, "/api/v1/auth/logout");
        }
        finally
        {
            Token = null;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        if (Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body2 = await response.Content.ReadAsStringAsync();
            throw new ApiException((int)response.StatusCode, string.IsNullOrWhiteSpace(body2) ? response.ReasonPhrase ?? "Request failed" : body2);
        }
        return response;
    }
}
