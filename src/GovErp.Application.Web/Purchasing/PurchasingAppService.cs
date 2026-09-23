using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Purchasing.Contracts;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Purchasing;

/// <summary>Заказы на чтение: строки дополняются encumbrance той же строки (та же связка, что и в IReferenceAppService.GetPurchaseOrdersAsync).</summary>
public sealed class PurchasingAppService(ITenantOperationRunner runner) : IPurchasingAppService
{
    public Task<IReadOnlyList<PurchaseOrderListItemVm>> ListOrdersAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<PurchaseOrderListItemVm>>(actor, async (sp, token) =>
        {
            var vendors = sp.GetRequiredService<IVendorRepository>();
            var encumbrances = sp.GetRequiredService<IEncumbranceRepository>();
            var result = new List<PurchaseOrderListItemVm>();
            foreach (var po in (await sp.GetRequiredService<IPurchaseOrderRepository>().ListAsync(token)).OrderBy(p => p.Number, StringComparer.Ordinal))
            {
                var vendor = await vendors.FindAsync(po.VendorId, token);
                var remaining = 0m;
                foreach (var line in po.Lines)
                {
                    var e = await encumbrances.FindByPoLineAsync(po.LineRef(line.LineNo), token);
                    remaining += e?.Remaining.Amount ?? line.Amount.Amount;
                }

                result.Add(new PurchaseOrderListItemVm(po.Number, vendor?.Name ?? "?", po.Status.ToString(), po.Total.Amount, remaining));
            }

            return result;
        }, ct);

    public Task<PurchaseOrderDetailVm> GetOrderAsync(string number, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var po = await sp.GetRequiredService<IPurchaseOrderRepository>().FindByNumberAsync(number, token)
                ?? throw new NotFoundException($"Purchase order {number} not found.");
            var vendor = await sp.GetRequiredService<IVendorRepository>().FindAsync(po.VendorId, token);
            var invoices = sp.GetRequiredService<IVendorInvoiceRepository>();
            var encumbrances = sp.GetRequiredService<IEncumbranceRepository>();
            var lines = new List<PurchaseOrderLineDetailVm>();
            foreach (var line in po.Lines.OrderBy(l => l.LineNo))
            {
                var e = await encumbrances.FindByPoLineAsync(po.LineRef(line.LineNo), token);
                lines.Add(new PurchaseOrderLineDetailVm(line.LineNo, line.Account.ToString(), line.Amount.Amount,
                    e is null ? null : await BudgetMapping.ToEncumbranceVmAsync(e, invoices, token)));
            }

            return new PurchaseOrderDetailVm(po.Number, po.VendorId, vendor?.Name ?? "?", po.Status.ToString(), po.Total.Amount, lines);
        }, ct);
}
