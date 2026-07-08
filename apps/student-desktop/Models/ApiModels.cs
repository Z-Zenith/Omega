using System;
using System.Collections.Generic;

namespace StudentDesktop.Models;

public record LoginRequest(string Identifier, string Password, string TotpCode, string? DeviceInfo);

public record LoginResponse(string Token, Guid UserId, Guid SessionId, string AccountType, string FullName);

public record CalendarItemDto(string Kind, Guid Id, string Title, DateTime Start, DateTime End, string? Extra);

public record MyCalendarResponse(List<CalendarItemDto> Items);

public record EventDto(Guid Id, string Title, DateTime StartTime, DateTime EndTime, bool IsRegistered);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string TotpCode);

// SDA-15
public record InternalMarkDto(Guid SubjectId, string SubjectName, decimal Marks, DateTime? PublishedAt);

public record ExternalMarkDto(Guid SubjectId, string SubjectName, string Grade, DateTime? ApprovedAt);

public record MyMarksResponse(List<InternalMarkDto> InternalMarks, List<ExternalMarkDto> ExternalMarks);

// SDA-03/SDA-04
public record WhitelistSiteDto(Guid Id, string Url, DateTime ApprovedAt);

public record WhitelistResponse(List<WhitelistSiteDto> Sites);

// SDA-08, SEK-03
// SDA-19: outgoing links as parsed by SEK's own extractOutgoingLinks, forwarded as-is.
public record NoteLinkInput(Guid ToNoteId, string Anchor);

public record CreateNoteRequest(string Title, string ContentMarkdown, Guid? Id = null, IReadOnlyList<NoteLinkInput>? Links = null);

public record UpdateNoteRequest(string Title, string ContentMarkdown, IReadOnlyList<NoteLinkInput>? Links = null);

public record NoteDto(Guid Id, string Title, string ContentMarkdown, DateTime CreatedAt, DateTime UpdatedAt);

public record NoteSummaryDto(Guid Id, string Title, DateTime UpdatedAt);

// SDA-11: request/response shapes for the auto-submit-on-exit endpoint
// (POST /api/v1/assignments/{id}/submissions/auto-submit). SubmissionFormat mirrors the
// backend's AssignmentType enum, serialized as a string (see Program.cs JsonStringEnumConverter).
public record SubmitAssignmentRequest(string ContentUrl, string SubmissionFormat);

public record SubmissionDto(Guid Id, Guid AssignmentId, Guid StudentId, string ContentUrl, DateTime SubmittedAt, bool IsLate, bool IsAutosubmitted);

// SDA-24, DMS-01: mirrors services/backend-api's MessageResponse/ThreadSummaryResponse
// field-for-field (see MessagingController.cs) — same shapes DmsBridge forwards to the
// DMS host bundle over the JS bridge.
public record SendMessageRequest(string Content);

public record DmsMessageDto(Guid Id, Guid ThreadId, Guid SenderId, string Content, DateTime SentAt, DateTime? ReadAt);

public record DmsThreadSummaryDto(Guid Id, Guid StudentId, Guid TeacherId, DateTime CreatedAt, DmsMessageDto? LastMessage);
