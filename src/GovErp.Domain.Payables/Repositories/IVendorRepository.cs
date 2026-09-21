using GovErp.Domain.Payables.Entities;
namespace GovErp.Domain.Payables.Repositories;

public interface IVendorRepository
{
    Task<Vendor?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Vendor>> ListAsync(CancellationToken ct = default);
}
