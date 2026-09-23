using GovErp.Domain.Payables.Entities;
namespace GovErp.Domain.Payables.Repositories;

public interface IVendorInvoiceRepository
{
    Task<VendorInvoice?> FindAsync(Guid id, CancellationToken ct = default);
    /// <summary>По регистрационному номеру (AP-2026-…) — им журнал ссылается на инвойс (SourceRef).</summary>
    Task<VendorInvoice?> FindByReferenceAsync(string reference, CancellationToken ct = default);
    Task<IReadOnlyList<VendorInvoice>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsDuplicateAsync(Guid vendorId, string normalizedNumber, Guid? excludingInvoiceId, CancellationToken ct = default);
    Task AddAsync(VendorInvoice invoice, CancellationToken ct = default);
}
