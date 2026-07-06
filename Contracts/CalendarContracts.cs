namespace BackendApi.Contracts;

public record CreateEventRequest(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    List<int>? RestrictedYears,
    List<Guid>? RestrictedDepartments);

public record EventDto(Guid Id, string Title, DateTime StartTime, DateTime EndTime, bool IsRegistered);

public record RegisterForEventResponse(Guid EventId, Guid StudentId, DateTime RegisteredAt);

// Kind is one of: college_event | todo | custom_entry | class_session.
// A registered college event is a college_event item with Extra containing "registered=true",
// not a separate parallel list.
public record CalendarItemDto(string Kind, Guid Id, string Title, DateTime Start, DateTime End, string? Extra);

public record MyCalendarResponse(List<CalendarItemDto> Items);
