using GovErp.Application.Web.Tenancy;
using GovErp.Infrastructure.Master;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Tenancy;

/// <summary>
/// Reads Master.Users of its tenant. Roles is a JSON column; entities are materialized first, then projected (EF does not translate List{string} in projections).
/// </summary>
public sealed class MasterTenantUserDirectory(MasterDbContext master) : ITenantUserDirectory
{
    public async Task<IReadOnlyList<TenantUserRecord>> ListAsync(TenantId tenantId, CancellationToken ct = default) =>
        (await master.Users.Where(u => u.TenantId == tenantId.Value).OrderBy(u => u.UserName).ToListAsync(ct))
            .Select(u => new TenantUserRecord(u.Id, u.UserName, u.DisplayName, u.Roles, u.DepartmentCode))
            .ToList();
}
