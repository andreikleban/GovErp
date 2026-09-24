namespace GovErp.Application.Web.Common;

/// <summary>
/// Who runs the use case. Built by Web from the authenticated principal; use cases receive it as a parameter (NM-16).
/// </summary>
public sealed record ActorContext(TenantId TenantId, UserId UserId, string UserName, IReadOnlySet<string> Roles, string? DepartmentCode)
{
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);
    public string TenantKey => TenantId.Value;
    public Guid UserKey => UserId.Value;

    public static ActorContext Create(string tenantId, Guid userId, string userName, IReadOnlySet<string> roles, string? departmentCode) =>
        new(new TenantId(tenantId), new UserId(userId), userName, roles, departmentCode);
}
