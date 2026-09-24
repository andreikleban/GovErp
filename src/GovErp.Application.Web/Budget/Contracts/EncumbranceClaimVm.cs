namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>An encumbrance liquidation claim or billing claim with the number of the invoice that holds (or held) it.</summary>
public sealed record EncumbranceClaimVm(Guid InvoiceId, string InvoiceReference, decimal Amount, string Status);
