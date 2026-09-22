using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

public sealed class EfReferenceDataRepository(GovErpDbContext db) : IReferenceDataRepository
{
    public Task<Department?> FindDepartmentAsync(DepartmentCode code, CancellationToken ct = default) =>
        db.Departments.SingleOrDefaultAsync(d => d.Code == code, ct);

    public Task<ObjectCodeDefinition?> FindObjectAsync(ObjectCode code, CancellationToken ct = default) =>
        db.ObjectCodes.SingleOrDefaultAsync(o => o.Code == code, ct);

    public async Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken ct = default) =>
        await db.Departments.OrderBy(d => d.Code).ToListAsync(ct);

    public async Task<IReadOnlyList<ObjectCodeDefinition>> ListObjectsAsync(CancellationToken ct = default) =>
        await db.ObjectCodes.OrderBy(o => o.Code).ToListAsync(ct);
}
