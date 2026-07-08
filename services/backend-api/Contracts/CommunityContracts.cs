using BackendApi.Data.Entities;

namespace BackendApi.Contracts;

public record CreateGroupRequest(string Name, GroupType Type, Guid? SectionId);

public record GroupDto(Guid Id, string Name, string Type, Guid? SectionId);

public record MyGroupsResponse(List<GroupDto> Groups);

public record CreatePostRequest(string Content);

public record GroupPostDto(Guid Id, Guid GroupId, Guid AuthorId, string Content, DateTime CreatedAt);

public record CreateMaterialRequest(string Title, string FileUrl, Guid? SubjectId, Guid? GroupId);

public record MaterialDto(Guid Id, string Title, string FileUrl, Guid? SubjectId, Guid? GroupId, Guid UploadedBy, DateTime UploadedAt);

// SDA-18 — course & teacher info per enrolled subject.
public record CourseInfoDto(Guid SubjectId, string Code, string Name, Guid? TeacherId, string? TeacherName);

// SDA-17 — feedback about a teacher/course. Attributable to the teacher it was submitted
// against (teacher_feedback has no subject_id column — a student's feedback is scoped to
// a teacher, which SDA-18's course list already resolves back to a specific course for them).
public record SubmitTeacherFeedbackRequest(Guid TeacherId, int Rating, string? Comments);

public record TeacherFeedbackDto(Guid Id, Guid StudentId, Guid TeacherId, int Rating, string? Comments, DateTime SubmittedAt);
