using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL repository of encumbrances for the current tenant.
/// </summary>
public sealed class EfEncumbranceRepository(GovErpDbContext db) : IEncumbranceRepository
{
    public Task<Encumbrance?> FindByPoLineAsync(string poLineRef, CancellationToken ct = default) =>
        db.Encumbrances.SingleOrDefaultAsync(e => e.PoLineRef == poLineRef, ct);

    /// <summary>
    /// Owner of the liquidation claim or billing claim.
    /// </summary>
    public Task<Encumbrance?> FindByClaimAsync(Guid claimId, CancellationToken ct = default) =>
        db.Encumbrances.SingleOrDefaultAsync(e => e.Claims.Any(c => c.Id == claimId) || e.BillingClaims.Any(c => c.Id == claimId), ct);

    public async Task<IReadOnlyList<Encumbrance>> ListAsync(CancellationToken ct = default) =>
        await db.Encumbrances.OrderBy(e => e.PoLineRef).ToListAsync(ct);

    public async Task AddAsync(Encumbrance encumbrance, CancellationToken ct = default) =>
        await db.Set<Encumbrance>().AddAsync(encumbrance, ct);
}
