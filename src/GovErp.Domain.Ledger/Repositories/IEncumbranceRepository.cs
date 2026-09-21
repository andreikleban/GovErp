using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IEncumbranceRepository
{
    Task<Encumbrance?> FindByPoLineAsync(string poLineRef, CancellationToken ct = default);
    Task AddAsync(Encumbrance encumbrance, CancellationToken ct = default);
}
