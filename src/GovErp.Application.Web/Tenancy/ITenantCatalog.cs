namespace GovErp.Application.Web.Tenancy;

public interface ITenantCatalog
{
    Task<TenantInfo?> FindAsync(TenantId id, CancellationToken ct = default);
    Task<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>Строка подключения runtime-пользователя тенанта; секрет не покидает Infrastructure.</summary>
    string RuntimeConnectionString(TenantInfo tenant);
}
