using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Reference;
using GovErp.Application.Web.Reference.Commands;
using GovErp.Application.Web.Reference.Contracts;
using GovErp.Infrastructure.Seed;

namespace GovErp.Application.Web.Tests.Reference;

[Collection("sql")]
public sealed class RuleVersionTests(SqlServerFixture fixture)
{
    private const string Water = "501-5000-53100";
    private const string Police = "701-3000-53100-G-COPS-26";

    private static RuleVm Current(RuleSetVm set, string ruleId) => set.Rules.Single(r => r.RuleId == ruleId && r.IsCurrent);

    private static NewRuleVersionCommand Raise(RuleVm source, string threshold) =>
        new(TenantDriver.Env(), source.Id, new Dictionary<string, string> { ["threshold"] = threshold }, null, SpringfieldData.Jun15,
            "Council raised the procurement threshold");

    [Fact]
    public async Task New_version_becomes_current_and_the_engine_applies_it()
    {
        var t = await fixture.CreateTenantAsync();
        var reference = t.Service<IRuleAppService>();
        var before = await reference.GetRulesAsync(t.Clerk);
        var source = Current(before, "PROCUREMENT_THRESHOLD");

        // 26,000 without a PO: under version 1 (threshold 25,000) it is a procurement Soft Stop.
        var invoice = await t.CreateAsync(26_000m, null, (Water, 26_000m, null));
        var first = await t.Service<IInvoiceAppService>().ValidateAsync(new InvoiceActionCommand(TenantDriver.Env(), invoice.Id), t.Clerk);
        first.Value!.LastEvaluation!.Outcomes.Should().Contain(o => o.RuleId == "PROCUREMENT_THRESHOLD" && o.Severity == "SoftStop");

        var created = await t.Service<IRuleAppService>().CreateVersionAsync(Raise(source, "200000"), t.FinanceDirector);

        created.IsAccepted.Should().BeTrue(created.Reason);
        created.Value!.Version.Should().Be(source.Version + 1);
        var after = await reference.GetRulesAsync(t.Clerk);
        Current(after, "PROCUREMENT_THRESHOLD").Parameters["threshold"].Should().Be("200000");
        after.Rules.Single(r => r.Id == source.Id).IsCurrent.Should().BeFalse();       // the previous version stays in history
        after.CurrentFingerprint.Should().NotBe(before.CurrentFingerprint);

        var second = await t.Service<IInvoiceAppService>().ValidateAsync(new InvoiceActionCommand(TenantDriver.Env(), invoice.Id), t.Clerk);
        second.Value!.LastEvaluation!.Outcomes.Should().NotContain(o => o.RuleId == "PROCUREMENT_THRESHOLD" && o.Severity == "SoftStop");
    }

    [Fact]
    public async Task Rule_change_after_approval_sends_the_invoice_back_to_approval()
    {
        var t = await fixture.CreateTenantAsync();
        var invoice = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(invoice.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(invoice.Id);

        var source = Current(await t.Service<IRuleAppService>().GetRulesAsync(t.Clerk), "PROCUREMENT_THRESHOLD");
        (await t.Service<IRuleAppService>().CreateVersionAsync(Raise(source, "30000"), t.FinanceDirector)).IsAccepted.Should().BeTrue();

        var refused = await t.PostAsync(invoice.Id);
        refused.Status.Should().Be(CommandStatus.Refused);
        refused.Reason.Should().Contain("REVALIDATION_REQUIRED");
        (await t.GetAsync(invoice.Id)).Status.Should().Be("Submitted");
    }

    [Fact]
    public async Task Rule_detail_shows_the_description_the_current_version_and_its_history()
    {
        var t = await fixture.CreateTenantAsync();
        var reference = t.Service<IRuleAppService>();
        var source = Current(await reference.GetRulesAsync(t.Clerk), "PROCUREMENT_THRESHOLD");
        (await t.Service<IRuleAppService>().CreateVersionAsync(Raise(source, "30000"), t.FinanceDirector)).IsAccepted.Should().BeTrue();

        var detail = await reference.GetRuleDetailAsync("PROCUREMENT_THRESHOLD", t.Clerk);
        detail.Description.Title.Should().NotBeNullOrWhiteSpace();
        detail.Current!.Parameters["threshold"].Should().Be("30000");
        detail.Versions.Select(v => v.Version).Should().Equal(2, 1);

        var explained = await reference.ExplainRuleAsync("PROCUREMENT_THRESHOLD", Application.Web.Explanation.ExplanationAudience.Auditor, t.Clerk);
        explained.Text.Should().Contain("threshold = 30000");                // the template (the provider in tests is Template) sees the current version
        await Assert.ThrowsAsync<Application.Web.Common.NotFoundException>(() => reference.GetRuleDetailAsync("NO_SUCH_RULE", t.Clerk));
    }

    [Fact]
    public async Task Only_the_finance_director_changes_rules_and_parameters_stay_fixed_by_the_rule()
    {
        var t = await fixture.CreateTenantAsync();
        var rules = t.Service<IRuleAppService>();
        var source = Current(await t.Service<IRuleAppService>().GetRulesAsync(t.Clerk), "PROCUREMENT_THRESHOLD");

        (await rules.CreateVersionAsync(Raise(source, "30000"), t.BudgetOfficer)).Status.Should().Be(CommandStatus.Forbidden);
        (await rules.CreateVersionAsync(Raise(source, "a lot"), t.FinanceDirector)).Status.Should().Be(CommandStatus.Refused);
        (await rules.CreateVersionAsync(Raise(source, "30000") with { Reason = " " }, t.FinanceDirector)).Status.Should().Be(CommandStatus.Refused);
        var unknownKey = Raise(source, "30000") with { Parameters = new Dictionary<string, string> { ["limit"] = "30000" } };
        (await rules.CreateVersionAsync(unknownKey, t.FinanceDirector)).Status.Should().Be(CommandStatus.Refused);

        var rulesAfter = await t.Service<IRuleAppService>().GetRulesAsync(t.Clerk);
        rulesAfter.Rules.Count(r => r.RuleId == "PROCUREMENT_THRESHOLD").Should().Be(1);   // the refusals saved nothing
    }
}
