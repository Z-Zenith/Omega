namespace BackendApi.Contracts;

// SDA-18. TeacherId/TeacherName are guaranteed present (not nullable) — every row here
// comes from a TeacherSectionAssignment, which by definition always names a teacher, so
// "every enrolled subject has a non-empty ... teacher-info entry" holds by construction.
public record MySubjectDto(Guid SubjectId, string SubjectCode, string SubjectName, Guid TeacherId, string TeacherName);
