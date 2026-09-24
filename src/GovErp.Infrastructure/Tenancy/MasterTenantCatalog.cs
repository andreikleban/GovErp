using System.Globalization;
using GovErp.Application.Web.Tenancy;
using GovErp.Infrastructure.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GovErp.Infrastructure.Tenancy;

public sealed class MasterTenantCatalog(MasterDbContext master, IOptions<TenancyOptions> options) : ITenantCatalog
{
    public async Task<TenantInfo?> FindAsync(TenantId id, CancellationToken ct = default) =>
        await master.Tenants.Where(t => t.Id == id.Value)
            .Select(t => new TenantInfo(new TenantId(t.Id), t.Name, t.DatabaseName, t.IsDemo)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken ct = default) =>
        await master.Tenants.OrderBy(t => t.Name).Select(t => new TenantInfo(new TenantId(t.Id), t.Name, t.DatabaseName, t.IsDemo)).ToListAsync(ct);

    /// <summary>The secret is read from configuration by the tenant's CredentialKey and does not leave Infrastructure.</summary>
    public string RuntimeConnectionString(TenantInfo tenant)
    {
        var key = master.Tenants.Where(t => t.Id == tenant.Id.Value).Select(t => t.CredentialKey).Single();
        var credential = options.Value.Credentials.GetValueOrDefault(key)
            ?? throw new InvalidOperationException($"No runtime credential configured for tenant {tenant.Id}.");
        return string.Format(CultureInfo.InvariantCulture, options.Value.RuntimeConnectionTemplate, tenant.DatabaseName, credential.User, credential.Password);
    }
}
