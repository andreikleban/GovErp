namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>
/// A budget reservation with the number of the invoice that holds (or held) it.
/// </summary>
public sealed record BudgetReservationVm(Guid InvoiceId, string InvoiceReference, decimal Amount, string Status);
