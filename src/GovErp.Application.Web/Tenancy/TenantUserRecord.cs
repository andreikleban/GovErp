namespace GovErp.Application.Web.Tenancy;

/// <summary>A tenant user from Master (read-only, DDD-7: not domain). Roles are the same strings as in ActorContext.Roles.</summary>
public sealed record TenantUserRecord(Guid Id, string UserName, string DisplayName, IReadOnlyList<string> Roles, string? DepartmentCode);
