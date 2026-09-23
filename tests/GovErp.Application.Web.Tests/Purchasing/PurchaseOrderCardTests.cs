using GovErp.Application.Web.Common;
using GovErp.Application.Web.Purchasing;

namespace GovErp.Application.Web.Tests.Purchasing;

[Collection("sql")]
public sealed class PurchaseOrderCardTests(SqlServerFixture fixture)
{
    private const string Po = "PO-2026-0451";
    private const string Police = "701-3000-53100-G-COPS-26";

    [Fact]
    public async Task Po_backed_invoice_appears_as_liquidation_and_billing_claims_on_the_order_line()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(164_800m, Po, (Police, 164_800m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await t.GetAsync(inv.Id)).Reference;

        var order = await t.Service<IPurchasingAppService>().GetOrderAsync(Po, t.Clerk);
        order.Number.Should().Be(Po);
        var line = order.Lines.Should().ContainSingle().Subject;
        line.Encumbrance.Should().NotBeNull();
        var liquidation = line.Encumbrance!.Claims.Should().ContainSingle().Subject;
        liquidation.InvoiceId.Should().Be(inv.Id);
        liquidation.InvoiceReference.Should().Be(reference);
        liquidation.Amount.Should().Be(160_000m);
        liquidation.Status.Should().Be("Held");
        var billing = line.Encumbrance.BillingClaims.Should().ContainSingle().Subject;
        billing.InvoiceReference.Should().Be(reference);
        billing.Amount.Should().Be(164_800m);
    }

    [Fact]
    public async Task Unknown_purchase_order_number_is_not_found()
    {
        var t = await fixture.CreateTenantAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => t.Service<IPurchasingAppService>().GetOrderAsync("PO-9999-9999", t.Clerk));
    }

    [Fact]
    public async Task List_shows_every_order_with_its_remaining_encumbrance()
    {
        var t = await fixture.CreateTenantAsync();
        var list = await t.Service<IPurchasingAppService>().ListOrdersAsync(t.Clerk);
        list.Should().Contain(o => o.Number == Po && o.Remaining == 160_000m && o.Total == 160_000m);
    }
}
