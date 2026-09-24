using GovErp.Application.Web.Budget.Contracts;

namespace GovErp.Application.Web.Purchasing.Contracts;

/// <summary>A purchase order line with that line's encumbrance (liquidation and billing claims with invoice numbers).</summary>
public sealed record PurchaseOrderLineDetailVm(int LineNo, string Account, decimal Amount, EncumbranceVm? Encumbrance);
