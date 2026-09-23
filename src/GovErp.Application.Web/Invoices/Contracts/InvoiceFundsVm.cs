namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>
/// Что инвойс удерживает в бюджете и по PO. Reserved — удерживаемые резервы бюджета, LiquidationClaimed — удерживаемые
/// claims ликвидации encumbrance, BillingClaimed — удерживаемые billing claims PO; Consumed — погашенные при Post
/// резервы и claims ликвидации (то, что стало actuals).
/// </summary>
public sealed record InvoiceFundsVm(decimal Reserved, decimal LiquidationClaimed, decimal BillingClaimed, decimal Consumed)
{
    public static readonly InvoiceFundsVm Empty = new(0m, 0m, 0m, 0m);
}
