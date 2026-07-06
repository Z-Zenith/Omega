using BackendApi.Data.Entities;

namespace BackendApi.Contracts;

public record CreateGroupRequest(string Name, GroupType Type, Guid? SectionId);

public record GroupDto(Guid Id, string Name, string Type, Guid? SectionId);

public record MyGroupsResponse(List<GroupDto> Groups);

public record CreatePostRequest(string Content);

public record GroupPostDto(Guid Id, Guid GroupId, Guid AuthorId, string Content, DateTime CreatedAt);
