namespace GovErp.Application.Web.Tenancy;

/// <summary>Пользователи текущего тенанта из Master (runtime-соединение с Master read-only, чтения это не мешает).</summary>
public interface ITenantUserDirectory
{
    Task<IReadOnlyList<TenantUserRecord>> ListAsync(TenantId tenantId, CancellationToken ct = default);
}
