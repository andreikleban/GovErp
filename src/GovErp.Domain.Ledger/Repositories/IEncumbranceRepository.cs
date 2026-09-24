using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IEncumbranceRepository
{
    Task<Encumbrance?> FindByPoLineAsync(string poLineRef, CancellationToken ct = default);
    /// <summary>The encumbrance that owns the liquidation claim or billing claim with this id.</summary>
    Task<Encumbrance?> FindByClaimAsync(Guid claimId, CancellationToken ct = default);
    /// <summary>All tenant encumbrances: the Budget › Encumbrances screen and the per-account filter for the budget line card.</summary>
    Task<IReadOnlyList<Encumbrance>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Encumbrance encumbrance, CancellationToken ct = default);
}
