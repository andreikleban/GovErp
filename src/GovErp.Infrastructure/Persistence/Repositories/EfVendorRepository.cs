using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

public sealed class EfVendorRepository(GovErpDbContext db) : IVendorRepository
{
    public Task<Vendor?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.Vendors.SingleOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IReadOnlyList<Vendor>> ListAsync(CancellationToken ct = default) =>
        await db.Vendors.OrderBy(v => v.Name).ToListAsync(ct);
}
