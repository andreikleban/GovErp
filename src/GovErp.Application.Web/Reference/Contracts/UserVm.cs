namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// A tenant user as shown on screen.
/// </summary>
public sealed record UserVm(Guid Id, string UserName, string DisplayName, IReadOnlyList<string> Roles, string? DepartmentCode);
