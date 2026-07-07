using System;
using System.Collections.Generic;

namespace StudentDesktop.Models;

public record LoginRequest(string Identifier, string Password, string TotpCode, string? DeviceInfo);

public record LoginResponse(string Token, Guid UserId, Guid SessionId, string AccountType, string FullName);

public record CalendarItemDto(string Kind, Guid Id, string Title, DateTime Start, DateTime End, string? Extra);

public record MyCalendarResponse(List<CalendarItemDto> Items);

public record EventDto(Guid Id, string Title, DateTime StartTime, DateTime EndTime, bool IsRegistered);

// SDA-15
public record InternalMarkDto(Guid SubjectId, string SubjectName, decimal Marks, DateTime? PublishedAt);

public record ExternalMarkDto(Guid SubjectId, string SubjectName, string Grade, DateTime? ApprovedAt);

public record MyMarksResponse(List<InternalMarkDto> InternalMarks, List<ExternalMarkDto> ExternalMarks);
