namespace BackendApi.Contracts;

// SDA-17. teacher_feedback (docs/campus-platform-db-api-schema.md §1.12) has no
// subject_id column — only teacher_id — so this stays teacher-scoped, not course-scoped,
// within the current frozen schema.
public record SubmitTeacherFeedbackRequest(Guid TeacherId, int Rating, string? Comments);

public record TeacherFeedbackDto(Guid Id, Guid TeacherId, int Rating, string? Comments, DateTime SubmittedAt);

// The subject/course context here is display-only for the picker UI — it isn't
// persisted on the TeacherFeedback row itself (see SubmitTeacherFeedbackRequest).
public record MyTeacherDto(Guid TeacherId, string TeacherName, Guid SubjectId, string SubjectName);
