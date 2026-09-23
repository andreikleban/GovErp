namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>Карточка строки бюджета: суммы и поправки (Line), резервы с номерами инвойсов, encumbrance по этому счёту.</summary>
public sealed record BudgetLineDetailVm(BudgetLineVm Line, IReadOnlyList<BudgetReservationVm> Reservations, IReadOnlyList<EncumbranceVm> Encumbrances);
