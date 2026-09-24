namespace GovErp.Application.Web.Tenancy;

/// <summary>
/// One tenant as registered in the catalog.
/// </summary>
public sealed record TenantInfo(TenantId Id, string Name, string DatabaseName, bool IsDemo);
