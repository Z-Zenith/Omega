namespace BackendApi.Contracts;

public record GenerateTimetableRequest(Guid? DepartmentId);

public record TimetableSlotDto(
    Guid Id,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid SectionId,
    string SectionName,
    Guid SubjectId,
    string SubjectName,
    Guid TeacherId,
    string TeacherName,
    string? Room,
    bool ManuallyEdited);

public record PatchSlotRequest(Guid? TeacherId, int? DayOfWeek, TimeOnly? StartTime, TimeOnly? EndTime, string? Room);

public record CreateChangeRequestRequest(string Description);

public record ChangeRequestDto(Guid Id, string Description, string Status, DateTime RequestedAt);
