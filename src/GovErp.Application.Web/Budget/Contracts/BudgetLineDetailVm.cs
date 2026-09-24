namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>Budget line card: amounts and amendments (Line), reservations with invoice numbers, encumbrances on this account.</summary>
public sealed record BudgetLineDetailVm(BudgetLineVm Line, IReadOnlyList<BudgetReservationVm> Reservations, IReadOnlyList<EncumbranceVm> Encumbrances);
