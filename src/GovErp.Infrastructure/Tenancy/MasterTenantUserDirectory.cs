using GovErp.Application.Web.Tenancy;
using GovErp.Infrastructure.Master;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Tenancy;

/// <summary>Читает Master.Users своего тенанта. Roles — JSON-колонка; материализуем сущности, затем проецируем (EF не переводит List{string} в проекции).</summary>
public sealed class MasterTenantUserDirectory(MasterDbContext master) : ITenantUserDirectory
{
    public async Task<IReadOnlyList<TenantUserRecord>> ListAsync(TenantId tenantId, CancellationToken ct = default) =>
        (await master.Users.Where(u => u.TenantId == tenantId.Value).OrderBy(u => u.UserName).ToListAsync(ct))
            .Select(u => new TenantUserRecord(u.Id, u.UserName, u.DisplayName, u.Roles, u.DepartmentCode))
            .ToList();
}
