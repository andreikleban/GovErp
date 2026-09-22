namespace GovErp.Application.Web.Common;

/// <summary>Кто выполняет сценарий. Строится Web из аутентифицированного principal; сценарии получают его параметром (NM-16).</summary>
public sealed record ActorContext(TenantId TenantId, UserId UserId, string UserName, IReadOnlySet<string> Roles, string? DepartmentCode)
{
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);
    public string TenantKey => TenantId.Value;
    public Guid UserKey => UserId.Value;

    public static ActorContext Create(string tenantId, Guid userId, string userName, IReadOnlySet<string> roles, string? departmentCode) =>
        new(new TenantId(tenantId), new UserId(userId), userName, roles, departmentCode);
}
