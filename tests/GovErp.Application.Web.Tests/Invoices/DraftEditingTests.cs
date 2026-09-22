using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Infrastructure.Seed;

namespace GovErp.Application.Web.Tests.Invoices;

[Collection("sql")]
public sealed class DraftEditingTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Header_and_distributions_save_together()
    {
        var t = await fixture.CreateTenantAsync();
        var created = await t.CreateAsync(10m, null, ("101-6000-53100", 10m, null));
        var saved = await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(
            TenantDriver.Env(created.RowVersion), created.Id, created.Number, SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jul15, 25m, null,
            [new DistributionCommand("101-6000-53100", 15m, null), new DistributionCommand("202-4000-53100", 10m, null)]), t.Clerk);
        saved.IsAccepted.Should().BeTrue(saved.Reason);
        var loaded = await t.GetAsync(created.Id);
        loaded.Total.Should().Be(25m);
        loaded.Distributions.Select(d => (d.Account, d.Amount)).Should().Equal(("101-6000-53100", 15m), ("202-4000-53100", 10m));
    }

    [Fact]
    public async Task Line_sum_is_checked_on_submit_not_save()
    {
        var t = await fixture.CreateTenantAsync();
        var created = await t.CreateAsync(10m, null, ("101-6000-53100", 10m, null));
        var saved = await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(
            TenantDriver.Env(created.RowVersion), created.Id, created.Number, SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jul15, 100m, null,
            [new DistributionCommand("101-6000-53100", 40m, null)]), t.Clerk);
        saved.IsAccepted.Should().BeTrue(saved.Reason);
        (await t.SubmitAsync(created.Id)).Status.Should().Be(CommandStatus.Refused);
    }

    [Fact]
    public async Task Draft_without_grant_is_saved_and_validate_reports_segment()
    {
        var t = await fixture.CreateTenantAsync();
        var created = await t.CreateAsync(10m, null, ("701-6000-53100", 10m, null));
        var validated = await t.Service<IInvoiceAppService>().ValidateAsync(new InvoiceActionCommand(TenantDriver.Env(), created.Id), t.Clerk);
        validated.IsAccepted.Should().BeTrue();
        validated.Value!.LastEvaluation!.Outcomes.Should().Contain(o => o.RuleId == "SEG_REQUIRED");
    }

    [Fact]
    public async Task Submitted_invoice_cannot_be_edited()
    {
        var t = await fixture.CreateTenantAsync();
        var created = await t.CreateAsync(10m, null, ("101-6000-53100", 10m, null));
        (await t.SubmitAsync(created.Id)).IsAccepted.Should().BeTrue();
        var v = await t.GetAsync(created.Id);
        var updated = await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(
            TenantDriver.Env(v.RowVersion), v.Id, v.Number, SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jul15, 11m, null,
            [new DistributionCommand("101-6000-53100", 11m, null)]), t.Clerk);
        updated.Status.Should().Be(CommandStatus.Refused);
    }

    [Fact]
    public async Task Distinct_document_dates_are_rejected_with_demo_limitation()
    {
        var t = await fixture.CreateTenantAsync();
        var created = await t.CreateAsync(10m, null, ("101-6000-53100", 10m, null));
        var updated = await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(
            TenantDriver.Env(created.RowVersion), created.Id, created.Number, SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jul15, SpringfieldData.Jun15, SpringfieldData.Jul15, 10m, null,
            [new DistributionCommand("101-6000-53100", 10m, null)]), t.Clerk);
        updated.Status.Should().Be(CommandStatus.Refused);
        updated.Reason.Should().NotBeNull();
        updated.Reason!.ToLowerInvariant().Should().Contain("demo").And.Contain("date");
    }
}
