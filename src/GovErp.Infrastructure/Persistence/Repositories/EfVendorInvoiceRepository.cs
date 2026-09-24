using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL repository of vendor invoices for the current tenant.
/// </summary>
public sealed class EfVendorInvoiceRepository(GovErpDbContext db) : IVendorInvoiceRepository
{
    public Task<VendorInvoice?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.VendorInvoices.SingleOrDefaultAsync(i => i.Id == id, ct);

    public Task<VendorInvoice?> FindByReferenceAsync(string reference, CancellationToken ct = default) =>
        db.VendorInvoices.SingleOrDefaultAsync(i => i.Reference == reference, ct);

    public async Task<IReadOnlyList<VendorInvoice>> ListAsync(CancellationToken ct = default) =>
        await db.VendorInvoices.OrderBy(i => i.Number).ToListAsync(ct);

    /// <summary>
    /// A preliminary check; a race is closed by the unique index on the computed NormalizedNumber column.
    /// </summary>
    public Task<bool> ExistsDuplicateAsync(Guid vendorId, string normalizedNumber, Guid? excludingInvoiceId, CancellationToken ct = default) =>
        db.VendorInvoices.AnyAsync(i => i.VendorId == vendorId && EF.Property<string>(i, "NormalizedNumber") == normalizedNumber
            && (excludingInvoiceId == null || i.Id != excludingInvoiceId), ct);

    public async Task AddAsync(VendorInvoice invoice, CancellationToken ct = default) =>
        await db.Set<VendorInvoice>().AddAsync(invoice, ct);
}
