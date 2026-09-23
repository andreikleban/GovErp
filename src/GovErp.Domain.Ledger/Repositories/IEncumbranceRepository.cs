using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IEncumbranceRepository
{
    Task<Encumbrance?> FindByPoLineAsync(string poLineRef, CancellationToken ct = default);
    /// <summary>Encumbrance, которому принадлежит claim ликвидации или billing claim с этим id.</summary>
    Task<Encumbrance?> FindByClaimAsync(Guid claimId, CancellationToken ct = default);
    /// <summary>Все encumbrance тенанта: экран Budget › Encumbrances и фильтр по счёту для карточки строки бюджета.</summary>
    Task<IReadOnlyList<Encumbrance>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Encumbrance encumbrance, CancellationToken ct = default);
}
