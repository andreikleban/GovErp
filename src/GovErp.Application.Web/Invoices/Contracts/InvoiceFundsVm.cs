namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>
/// What the invoice holds in the budget and on the PO. Reserved: budget reservations held; LiquidationClaimed: encumbrance
/// liquidation claims held; BillingClaimed: PO billing claims held; Consumed: reservations and liquidation claims
/// consumed at Post (what became actuals).
/// </summary>
public sealed record InvoiceFundsVm(decimal Reserved, decimal LiquidationClaimed, decimal BillingClaimed, decimal Consumed)
{
    public static readonly InvoiceFundsVm Empty = new(0m, 0m, 0m, 0m);
}
