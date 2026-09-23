namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>Claim ликвидации или billing claim encumbrance с номером инвойса, который его держит (или держал).</summary>
public sealed record EncumbranceClaimVm(Guid InvoiceId, string InvoiceReference, decimal Amount, string Status);
