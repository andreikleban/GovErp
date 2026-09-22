namespace GovErp.Application.Web.Common;

/// <summary>Кто выполняет сценарий. Строится Web из аутентифицированного principal; сценарии получают его параметром (NM-16).</summary>
public sealed record ActorContext(TenantId TenantId, UserId UserId, string UserName, IReadOnlySet<string> Roles, string? DepartmentCode)
{
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);
}
