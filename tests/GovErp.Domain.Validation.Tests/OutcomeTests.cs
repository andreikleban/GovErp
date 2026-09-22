using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class OutcomeTests
{
    [Fact]
    public void Evidence_is_copied_and_cannot_be_mutated_after_evaluation()
    {
        var inputs = new Dictionary<string, string> { ["amount"] = "10" };
        var definition = new RuleDefinition("TEST", 1, ValidationStep.TransactionPurpose,
            RuleLayer.Core, Severity.SoftStop, new Dictionary<string, string>(),
            [ApproverRole.FinanceDirector], new DateOnly(2026, 1, 1), null, "Test", "Resolve");
        var outcome = RuleOutcome.From(definition, Severity.SoftStop, 1, inputs, inputs);
        inputs["amount"] = "20";
        outcome.Inputs["amount"].Should().Be("10");
        outcome.Computed["amount"].Should().Be("10");
        ((IDictionary<string, string>)outcome.Inputs).Invoking(d => d.Add("x", "y"))
            .Should().Throw<NotSupportedException>();
        outcome.OutcomeRef.Should().NotBeEmpty();
    }

    [Fact]
    public void Unchecked_posting_is_not_permission_to_post_or_pay()
    {
        Capabilities.For(Severity.Allowed, null).CanPost.Should().BeFalse();
        Capabilities.For(Severity.Allowed, true).CanPost.Should().BeTrue();
        Capabilities.For(Severity.SoftStop, true).CanPost.Should().BeFalse();
        Capabilities.For(Severity.Allowed, true).CanPay.Should().BeFalse();
    }

    [Fact]
    public void Restore_reproduces_every_persisted_field()
    {
        var o = RuleOutcome.From(DemoRules.Rule("P", ValidationStep.TransactionPurpose, RuleLayer.State, Severity.SoftStop,
            overridableBy: [ApproverRole.FinanceDirector]), Severity.SoftStop, 1,
            new Dictionary<string, string> { ["amount"] = "1" }, new Dictionary<string, string> { ["x"] = "2" });
        var back = RuleOutcome.Restore(o.OutcomeRef, o.RuleId, o.RuleVersion, o.Step, o.Layer, o.DistributionLine, o.Severity,
            o.Inputs, o.Computed, o.Message, o.Resolution, o.OverridableBy, null);
        back.Should().BeEquivalentTo(o);
    }
}
