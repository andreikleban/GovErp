using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class OutcomeAggregationTests
{
    private static readonly RuleSetVersions Versions = RuleResolver.Default.Resolve(DemoRules.All(), SubjectBuilder.June15).Versions;
    private static RuleOutcome Outcome(Severity severity = Severity.SoftStop, string value = "100", int? line = 1) =>
        RuleOutcome.From(DemoRules.Rule("B", ValidationStep.BudgetAvailability, RuleLayer.Core, severity,
            overridableBy: [ApproverRole.BudgetOfficer]), severity, line,
            new Dictionary<string, string> { ["amount"] = value }, new Dictionary<string, string>(), "TEST.STOP");

    private static (ValidationSubject Subject, RuleOutcome Current) Bound()
    {
        var old = Outcome();
        var s = new SubjectBuilder().ApprovalCycle(Guid.NewGuid(), Versions.Fingerprint).Build();
        var id = Guid.NewGuid();
        var ov = new OverrideSnapshot("B", SubjectBuilder.Approver, "Approved exception", 1, 1, 1,
            s.Transaction.ApprovalCycleId, Versions.Fingerprint, id, ApproverRole.BudgetOfficer, old.OutcomeRef);
        return (s with { PreviousEvaluationId = id, PreviousOutcomes = [old], OverridesSoFar = [ov] }, Outcome());
    }

    [Fact]
    public void Persisted_outcome_binding_accepts_new_output_identity_and_preserves_original_severity()
    {
        var (subject, current) = Bound();
        current.OutcomeRef.Should().NotBe(subject.PreviousOutcomes[0].OutcomeRef);
        var result = OutcomeAggregation.Apply([current], subject, Versions);
        result.Overall.Should().Be(Severity.Allowed);
        result.WithOverrides[0].Severity.Should().Be(Severity.SoftStop);
        result.WithOverrides[0].IsOverridden.Should().BeTrue();
        current.IsOverridden.Should().BeFalse();
    }

    [Theory]
    [InlineData("evaluation")]
    [InlineData("outcome")]
    [InlineData("rule")]
    [InlineData("version")]
    [InlineData("line")]
    [InlineData("content")]
    [InlineData("cycle")]
    [InlineData("fingerprint")]
    [InlineData("role")]
    [InlineData("author")]
    [InlineData("reason")]
    [InlineData("evidence")]
    [InlineData("missing")]
    public void Invalid_override_binding_cannot_remove_stop(string mismatch)
    {
        var (s, current) = Bound();
        var ov = s.OverridesSoFar[0];
        ov = mismatch switch
        {
            "evaluation" => ov with { EvaluationId = Guid.NewGuid() },
            "outcome" => ov with { OutcomeRef = Guid.NewGuid() },
            "rule" => ov with { RuleId = "OTHER" },
            "version" => ov with { RuleVersion = 2 },
            "line" => ov with { DistributionLine = 2 },
            "content" => ov with { ContentVersion = 2 },
            "cycle" => ov with { ApprovalCycleId = Guid.NewGuid() },
            "fingerprint" => ov with { RuleFingerprint = "stale" },
            "role" => ov with { Role = ApproverRole.DepartmentHead },
            "author" => ov with { UserId = SubjectBuilder.Clerk },
            "reason" => ov with { Reason = " " },
            _ => ov
        };
        s = s with { OverridesSoFar = [ov] };
        if (mismatch == "evidence") current = Outcome(value: "200");
        if (mismatch == "missing") s = s with { PreviousOutcomes = [] };
        var result = OutcomeAggregation.Apply([current], s, Versions);
        result.Overall.Should().Be(Severity.SoftStop);
        result.WithOverrides[0].IsOverridden.Should().BeFalse();
    }

    [Fact]
    public void Overrides_issued_against_different_evaluations_both_bind()
    {
        var (s, _) = Bound();
        var first = s.OverridesSoFar[0];
        var olderOutcome = Outcome(line: 2);
        var olderEvaluation = Guid.NewGuid();
        var second = first with { EvaluationId = olderEvaluation, OutcomeRef = olderOutcome.OutcomeRef, DistributionLine = 2 };
        s = s with
        {
            OverridesSoFar = [first, second],
            PreviousEvaluations = [new PreviousEvaluation(olderEvaluation, [olderOutcome])],
        };

        var result = OutcomeAggregation.Apply([Outcome(line: 1), Outcome(line: 2)], s, Versions);

        result.Overall.Should().Be(Severity.Allowed);
        result.WithOverrides.Should().OnlyContain(o => o.IsOverridden);
    }

    [Fact]
    public void Hard_stop_always_wins_and_empty_outcomes_are_allowed()
    {
        var (s, current) = Bound();
        OutcomeAggregation.Apply([current, Outcome(Severity.HardStop)], s, Versions).Overall.Should().Be(Severity.HardStop);
        OutcomeAggregation.Apply([], s, Versions).Overall.Should().Be(Severity.Allowed);
    }
}
