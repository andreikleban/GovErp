namespace GovErp.Application.Web.Reference.Contracts;

public sealed record UserVm(Guid Id, string UserName, string DisplayName, IReadOnlyList<string> Roles, string? DepartmentCode);
