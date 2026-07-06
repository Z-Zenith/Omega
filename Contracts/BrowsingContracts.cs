namespace BackendApi.Contracts;

public record WhitelistSiteDto(Guid Id, string Url, DateTime ApprovedAt);

public record WhitelistResponse(List<WhitelistSiteDto> Sites);

public record CreateWhitelistRequestRequest(string Url);

public record WhitelistRequestDto(Guid Id, string Url, Guid RequestedBy, string Status, Guid? ReviewedBy);

public record ApproveWhitelistRequestResponse(Guid RequestId, string Status, WhitelistSiteDto Site);
