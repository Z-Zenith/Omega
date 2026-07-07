using BackendApi.Data.Entities;

namespace BackendApi.Contracts;

public record CreateGroupRequest(string Name, GroupType Type, Guid? SectionId);

public record GroupDto(Guid Id, string Name, string Type, Guid? SectionId);

public record MyGroupsResponse(List<GroupDto> Groups);

// AWA-06 — includes CreatedBy so Admin can see who created each group; the
// member-facing GroupDto deliberately omits it (not needed there).
public record AdminGroupDto(Guid Id, string Name, string Type, Guid? SectionId, Guid? CreatedBy);

public record AllGroupsResponse(List<AdminGroupDto> Groups);

public record CreatePostRequest(string Content);

public record GroupPostDto(Guid Id, Guid GroupId, Guid AuthorId, string Content, DateTime CreatedAt);

public record CreateMaterialRequest(string Title, string FileUrl, Guid? SubjectId, Guid? GroupId);

public record MaterialDto(Guid Id, string Title, string FileUrl, Guid? SubjectId, Guid? GroupId, Guid UploadedBy, DateTime UploadedAt);
