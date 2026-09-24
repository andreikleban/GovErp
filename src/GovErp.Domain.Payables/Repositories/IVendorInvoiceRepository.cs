using GovErp.Domain.Payables.Entities;
namespace GovErp.Domain.Payables.Repositories;

/// <summary>
/// Collection of vendor invoices.
/// </summary>
public interface IVendorInvoiceRepository
{
    Task<VendorInvoice?> FindAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// By registration number (AP-2026-…): the journal refers to the invoice by it (SourceRef).
    /// </summary>
    Task<VendorInvoice?> FindByReferenceAsync(string reference, CancellationToken ct = default);
    Task<IReadOnlyList<VendorInvoice>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsDuplicateAsync(Guid vendorId, string normalizedNumber, Guid? excludingInvoiceId, CancellationToken ct = default);
    Task AddAsync(VendorInvoice invoice, CancellationToken ct = default);
}
