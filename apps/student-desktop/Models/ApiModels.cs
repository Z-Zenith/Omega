using System;
using System.Collections.Generic;

namespace StudentDesktop.Models;

public record LoginRequest(string Identifier, string Password, string TotpCode, string? DeviceInfo);

public record LoginResponse(string Token, Guid UserId, Guid SessionId, string AccountType, string FullName);

public record CalendarItemDto(string Kind, Guid Id, string Title, DateTime Start, DateTime End, string? Extra);

public record MyCalendarResponse(List<CalendarItemDto> Items);

public record EventDto(Guid Id, string Title, DateTime StartTime, DateTime EndTime, bool IsRegistered);

// SDA-11: request/response shapes for the auto-submit-on-exit endpoint
// (POST /api/v1/assignments/{id}/submissions/auto-submit). SubmissionFormat mirrors the
// backend's AssignmentType enum, serialized as a string (see Program.cs JsonStringEnumConverter).
public record SubmitAssignmentRequest(string ContentUrl, string SubmissionFormat);

public record SubmissionDto(Guid Id, Guid AssignmentId, Guid StudentId, string ContentUrl, DateTime SubmittedAt, bool IsLate, bool IsAutosubmitted);
