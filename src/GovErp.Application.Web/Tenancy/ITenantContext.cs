namespace GovErp.Application.Web.Tenancy;

/// <summary>
/// The tenant of the current operation.
/// </summary>
public interface ITenantContext
{
    TenantId TenantId { get; }
    string ConnectionString { get; }
    bool IsInitialized { get; }
}
