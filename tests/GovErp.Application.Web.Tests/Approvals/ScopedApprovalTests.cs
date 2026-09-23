using GovErp.Application.Web.Approvals;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Posting;
using GovErp.Infrastructure.Seed;

namespace GovErp.Application.Web.Tests.Approvals;

[Collection("sql")]
public sealed class ScopedApprovalTests(SqlServerFixture fixture)
{
    private const string Fire = "701-6000-53100-G-COPS-26";
    private const string Police = "701-3000-53100-G-COPS-26";

    [Fact]
    public async Task Fire_chief_cannot_satisfy_the_police_step()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();

        var current = await t.GetAsync(inv.Id);
        var fire = await t.Service<IApprovalAppService>().ApproveAsync(Action(current), t.FireChief);
        fire.Status.Should().Be(CommandStatus.Forbidden);

        current = await t.GetAsync(inv.Id);
        (await t.Service<IApprovalAppService>().ApproveAsync(Action(current), t.PoliceChief)).IsAccepted.Should().BeTrue();
    }

    [Fact]
    public async Task Author_cannot_approve_their_own_invoice_even_with_the_role()
    {
        var t = await fixture.CreateTenantAsync();
        var author = new ActorContext(t.Tenant, new UserId(Guid.NewGuid()), "author", new HashSet<string> { Roles.ApClerk, Roles.DepartmentHead }, "6000");
        var created = await t.Service<IInvoiceAppService>().CreateDraftAsync(new CreateInvoiceCommand(
            TenantDriver.Env(), $"T-{Guid.NewGuid():N}"[..12], SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jul15, 30_000m, null,
            [new DistributionCommand(Fire, 30_000m, null)]), author);
        created.IsAccepted.Should().BeTrue(created.Reason);

        var submitted = await t.Service<IInvoiceAppService>().SubmitAsync(Action(created.Value!), author);
        submitted.IsAccepted.Should().BeTrue(submitted.Reason);
        var decision = await t.Service<IApprovalAppService>().ApproveAsync(Action(await t.GetAsync(created.Value!.Id)), author);
        decision.Status.Should().Be(CommandStatus.Forbidden);
        decision.Reason.Should().Contain("author");
    }

    [Fact]
    public async Task Override_without_a_reason_is_refused()
    {
        var t = await fixture.CreateTenantAsync();
        var current = await SubmittedSoftStop(t);
        var soft = current.LastEvaluation!.Outcomes.Single(o => o.Severity == "SoftStop" && o.OverriddenBy == null);
        var result = await t.Service<IApprovalAppService>().OverrideAsync(
            new OverrideCommand(TenantDriver.Env(current.RowVersion), current.Id, current.LastEvaluation.Id, soft.RuleId, soft.Line, " "), t.FinanceDirector);
        result.IsAccepted.Should().BeFalse();
        result.Reason.Should().Contain("Reason");
    }

    [Fact]
    public async Task Editing_the_document_makes_the_previous_override_inapplicable()
    {
        var t = await fixture.CreateTenantAsync();
        var current = await SubmittedSoftStop(t);
        var soft = current.LastEvaluation!.Outcomes.Single(o => o.RuleId == "PROCUREMENT_THRESHOLD");
        var granted = await t.Service<IApprovalAppService>().OverrideAsync(
            new OverrideCommand(TenantDriver.Env(current.RowVersion), current.Id, current.LastEvaluation.Id, soft.RuleId, soft.Line, "documented exception"), t.FinanceDirector);
        granted.IsAccepted.Should().BeTrue(granted.Reason);

        current = await t.GetAsync(current.Id);
        (await t.Service<IInvoiceAppService>().WithdrawAsync(new ReasonedActionCommand(TenantDriver.Env(current.RowVersion), current.Id, "change the amount"), t.Clerk))
            .IsAccepted.Should().BeTrue();
        current = await t.GetAsync(current.Id);
        (await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(
            TenantDriver.Env(current.RowVersion), current.Id, current.Number, SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jul15, 31_000m, null,
            [new DistributionCommand(Fire, 31_000m, null)]), t.Clerk)).IsAccepted.Should().BeTrue();

        (await t.SubmitAsync(current.Id)).IsAccepted.Should().BeTrue();
        var again = (await t.GetAsync(current.Id)).LastEvaluation!;
        again.Outcomes.Should().Contain(o => o.RuleId == "PROCUREMENT_THRESHOLD" && o.OverriddenBy == null);
    }

    [Fact]
    public async Task A_second_post_does_not_create_another_journal()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();

        var journal = await t.Service<IPostingAppService>().GetJournalAsync(inv.Id, t.Clerk);
        journal.Should().NotBeEmpty();
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeFalse();
        (await t.Service<IPostingAppService>().GetJournalAsync(inv.Id, t.Clerk)).Should().Equal(journal);
    }

    private static InvoiceActionCommand Action(GovErp.Application.Web.Invoices.Contracts.InvoiceVm invoice) =>
        new(TenantDriver.Env(invoice.RowVersion), invoice.Id);

    private static async Task<GovErp.Application.Web.Invoices.Contracts.InvoiceVm> SubmittedSoftStop(TenantDriver t)
    {
        var inv = await t.CreateAsync(30_000m, null, (Fire, 30_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var current = await t.GetAsync(inv.Id);
        current.LastEvaluation!.Outcomes.Should().Contain(o => o.RuleId == "PROCUREMENT_THRESHOLD" && o.Severity == "SoftStop");
        return current;
    }
}
