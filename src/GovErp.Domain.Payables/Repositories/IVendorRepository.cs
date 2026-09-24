using GovErp.Domain.Payables.Entities;
namespace GovErp.Domain.Payables.Repositories;

/// <summary>
/// Collection of vendors.
/// </summary>
public interface IVendorRepository
{
    Task<Vendor?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Vendor>> ListAsync(CancellationToken ct = default);
}
