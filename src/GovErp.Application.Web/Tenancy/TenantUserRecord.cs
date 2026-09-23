namespace GovErp.Application.Web.Tenancy;

/// <summary>Пользователь тенанта из Master (только чтение, DDD-7 — не домен). Roles — те же строки, что и в ActorContext.Roles.</summary>
public sealed record TenantUserRecord(Guid Id, string UserName, string DisplayName, IReadOnlyList<string> Roles, string? DepartmentCode);
