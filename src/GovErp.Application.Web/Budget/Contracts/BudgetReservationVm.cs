namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>Резерв бюджета с номером инвойса, который его держит (или держал).</summary>
public sealed record BudgetReservationVm(Guid InvoiceId, string InvoiceReference, decimal Amount, string Status);
