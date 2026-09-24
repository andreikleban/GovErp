using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class PipelineScenarioTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly EffectiveRuleSet Rules = RuleResolver.Default.Resolve(DemoRules.All(), SubjectBuilder.June15);
    private static readonly ValidationPipeline Pipeline = new(RuleCatalog.Default);
    private static EvaluationRecord Run(ValidationSubject s, EvaluationTrigger trigger = EvaluationTrigger.Manual) =>
        Pipeline.Evaluate(s, Rules, trigger, SubjectBuilder.Approver, At);
    private static ValidationSubject Healthy() => new SubjectBuilder().With(SubjectBuilder.Distribution(1,
        "101-6000-53100", 1000, budget: SubjectBuilder.Budget(100000, 0, 0))).Build();
    private static ValidationSubject Amended() => new SubjectBuilder().With(SubjectBuilder.Distribution(1,
        "701-6000-53100-G-COPS-26", 160000, budget: SubjectBuilder.Budget(388000, 132000, 96000))).Build();
    private static ValidationSubject BindOverride(ValidationSubject subject, string ruleId)
    {
        subject = subject with { Transaction = subject.Transaction with { RuleFingerprint = Rules.Fingerprint } };
        var previous = Run(subject);
        var outcome = previous.Outcomes.Single(x => x.RuleId == ruleId);
        return subject with { PreviousEvaluationId = previous.Id, PreviousOutcomes = previous.Outcomes,
            OverridesSoFar = [new(ruleId, SubjectBuilder.Approver, "Documented exception", outcome.RuleVersion,
                outcome.DistributionLine, subject.Transaction.ContentVersion, subject.Transaction.ApprovalCycleId,
                Rules.Fingerprint, previous.Id, ApproverRole.FinanceDirector, outcome.OutcomeRef)] };
    }
    private static ValidationSubject Approved(ValidationSubject s)
    {
        s = s with { Transaction = s.Transaction with { Status = "Approved", RuleFingerprint = Rules.Fingerprint },
            VersionsAtLastApproval = Rules.Versions };
        return s with { DetailedApprovals = [new(ApproverRole.DepartmentHead, SubjectBuilder.Approver,
            new DepartmentCode("6000"), s.Transaction.ContentVersion, s.Transaction.ApprovalCycleId,
            Rules.Fingerprint, Guid.NewGuid())] };
    }
    private static ValidationSubject Po(decimal amount) => new SubjectBuilder().PoBacked().With(
        SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", amount,
            budget: SubjectBuilder.Budget(500000, 100000, 160000),
            encumbrance: new("PO-2026-0451/1", Money.Of(160000), true,
                AuthorizedPoAmount: Money.Of(160000), CurrentInvoicePoAmount: Money.Of(amount)))).Build();

    [Fact]
    public void Scenario_NonPo_701_ExceedsAvailable_By13000_IsHardStop()
    {
        var r = Run(SubjectBuilder.Exercise());
        r.Overall.Should().Be(Severity.HardStop);
        r.Outcomes.Single(x => x.RuleId == "BUDGET_AVAILABILITY").Computed["overage"].Should().Be("13,000.00");
        r.Outcomes.Should().Contain(x => x.RuleId == "PROCUREMENT_THRESHOLD");
        r.ApprovalRoute.Should().NotBeEmpty();
        r.PostingPreview.Should().BeEmpty();
        r.Capabilities.Should().Be(new Capabilities(true, false, false, false, false));
    }

    [Fact]
    public void Scenario_NonPo_701_AfterAmendment13000_IsSoftStop_ProcurementThreshold()
    {
        var r = Run(Amended());
        r.Overall.Should().Be(Severity.SoftStop);
        r.Outcomes.Should().Contain(x => x.RuleId == "BUDGET_LOW_REMAINING" && x.Severity == Severity.Warning);
        r.Capabilities.CanSubmit.Should().BeTrue();
        r.PostingPreview.Should().HaveCount(2);
    }

    [Fact]
    public void Scenario_NonPo_701_AfterAmendment_WithOverride_IsWarning()
    {
        var r = Run(BindOverride(Amended(), "PROCUREMENT_THRESHOLD"));
        r.Overall.Should().Be(Severity.Warning);
        r.Outcomes.Single(x => x.RuleId == "PROCUREMENT_THRESHOLD").Severity.Should().Be(Severity.SoftStop);
        r.Outcomes.Single(x => x.RuleId == "PROCUREMENT_THRESHOLD").IsOverridden.Should().BeTrue();
    }

    [Theory]
    [InlineData(160000, Severity.Allowed, "0.00")]
    [InlineData(164800, Severity.Warning, "4,800.00")]
    [InlineData(172800, Severity.HardStop, "12,800.00")]
    public void Scenario_PoBacked_Tolerance_uses_authorized_amount(decimal amount, Severity severity, string excess)
    {
        var r = Run(Po(amount));
        r.Overall.Should().Be(severity);
        r.Outcomes.Single(x => x.RuleId == "PO_LIQUIDATION").Computed["excess"].Should().Be(excess);
        r.Outcomes.Should().NotContain(x => x.RuleId == "PROCUREMENT_THRESHOLD");
        if (severity != Severity.HardStop) r.PostingPreview.Should().HaveCount(4);
    }

    [Fact]
    public void Scenario_MultiFund_101_Overage_IsSoftStop_202_501_Allowed()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 12000, budget: SubjectBuilder.Budget(50000, 40000, 0)))
            .With(SubjectBuilder.Distribution(2, "202-4000-53100", 8000, budget: SubjectBuilder.Budget(25000, 5000, 0)))
            .With(SubjectBuilder.Distribution(3, "501-5000-53100", 10000, budget: SubjectBuilder.Budget(60000, 30000, 0))).Build();
        var r = Run(s);
        r.Overall.Should().Be(Severity.SoftStop);
        r.Outcomes.Where(x => x.RuleId == "BUDGET_AVAILABILITY").Should().ContainSingle(x => x.DistributionLine == 1);
        r.PostingPreview.Should().HaveCount(6);
        PostingPreviewBuilder.IsBalancedPerFund(r.PostingPreview).Should().BeTrue();
        r.PostingPreview.Should().Contain(x => x.Account.Fund.Value == "501" && x.Description.StartsWith("Expense"));
    }

    [Fact]
    public void Pipeline_HardStopAtStep2_SkipsSteps3To6_StillBuildsRoute()
    {
        var s = Healthy();
        s = s with { Distributions = [s.Distributions[0] with { Combination = new(false, false, "Missing") }] };
        var r = Run(s);
        r.Steps.Should().HaveCount(8);
        r.Steps.Where(x => (int)x.Step >= 3 && (int)x.Step <= 6).Should().OnlyContain(x => x.Status == StepExecutionStatus.Skipped);
        r.Steps.Single(x => x.Step == ValidationStep.ApprovalRequirements).Status.Should().Be(StepExecutionStatus.Executed);
        r.Outcomes.Should().NotContain(x => x.RuleId == "BUDGET_AVAILABILITY");
        r.ApprovalRoute.Should().NotBeEmpty();
    }

    [Fact]
    public void Post_requires_actual_eligibility_and_manual_never_fakes_post_pass()
    {
        var s = Approved(Healthy());
        var post = Run(s, EvaluationTrigger.Post);
        post.PostingCheck!.Passed.Should().BeTrue();
        post.Capabilities.CanPost.Should().BeTrue();
        var manual = Run(s);
        manual.PostingCheck.Should().BeNull();
        manual.Capabilities.CanPost.Should().BeFalse();
        Run(s with { DetailedApprovals = [], ApprovalsSoFar = [ApproverRole.DepartmentHead] }, EvaluationTrigger.Post)
            .Capabilities.CanPost.Should().BeFalse();
    }

    [Fact]
    public void Reapproval_after_rule_change_unblocks_post()
    {
        var s = Approved(Healthy());
        var stale = s with { VersionsAtLastApproval = Rules.Versions with { Fingerprint = "old" } };
        Run(stale, EvaluationTrigger.Post).Capabilities.CanPost.Should().BeFalse();
        Run(s, EvaluationTrigger.Post).Capabilities.CanPost.Should().BeTrue();
    }

    [Theory]
    [InlineData("Draft", true, true, false)]
    [InlineData("Submitted", false, false, true)]
    [InlineData("Approved", false, false, false)]
    [InlineData("Posted", false, false, false)]
    public void Capabilities_obey_document_lifecycle(string status, bool save, bool submit, bool approve)
    {
        var s = Healthy();
        s = s with { Transaction = s.Transaction with { Status = status, RuleFingerprint = Rules.Fingerprint } };
        var c = Run(s).Capabilities;
        c.CanSave.Should().Be(save);
        c.CanSubmit.Should().Be(submit);
        c.CanApprove.Should().Be(approve);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("line-zero")]
    [InlineData("amount-zero")]
    [InlineData("amount-negative")]
    [InlineData("total")]
    [InlineData("content")]
    [InlineData("author")]
    [InlineData("cycle")]
    [InlineData("status")]
    [InlineData("invoice-date")]
    [InlineData("service-date")]
    [InlineData("posting-date")]
    [InlineData("fiscal-year")]
    [InlineData("overflow")]
    public void Invalid_snapshots_fail_closed_with_explicit_skipped_steps(string invalid)
    {
        var s = Healthy();
        var d = s.Distributions[0];
        s = invalid switch
        {
            "empty" => s with { Distributions = [] },
            "duplicate" => s with { Distributions = [d, d] },
            "line-zero" => s with { Distributions = [d with { LineNo = 0 }] },
            "amount-zero" => s with { Distributions = [d with { Amount = Money.Zero }] },
            "amount-negative" => s with { Distributions = [d with { Amount = Money.Of(-1) }] },
            "total" => s with { Transaction = s.Transaction with { Total = Money.Of(1001) } },
            "content" => s with { Transaction = s.Transaction with { TransactionVersion = 0 } },
            "author" => s with { Transaction = s.Transaction with { CreatedBy = default } },
            "cycle" => s with { Transaction = s.Transaction with { Status = "Submitted", ApprovalCycleId = Guid.Empty } },
            "status" => s with { Transaction = s.Transaction with { Status = "Whatever" } },
            "invoice-date" => s with { Transaction = s.Transaction with { Date = SubjectBuilder.June15.AddDays(1) } },
            "service-date" => s with { Transaction = s.Transaction with { ServiceDate = null } },
            "posting-date" => s with { Transaction = s.Transaction with { PostingDate = SubjectBuilder.June15.AddDays(1) } },
            "fiscal-year" => s with { Distributions = [d with { Budget = d.Budget with { FiscalYear = 2027 } }] },
            "overflow" => s with { Distributions = [d with { Amount = Money.Of(9999999999999999) }, d with { LineNo = 2, Amount = Money.Of(9999999999999999) }] },
            _ => s
        };
        var r = Run(s, EvaluationTrigger.Post);
        r.Overall.Should().Be(Severity.HardStop);
        r.Capabilities.CanPost.Should().BeFalse();
        r.Steps.Should().HaveCount(8);
        r.Steps.Where(x => x.Step != ValidationStep.RequiredSegments).Should().OnlyContain(x => x.Status == StepExecutionStatus.Skipped);
        r.Outcomes.Should().Contain(x => x.RuleId == "VALIDATION_INPUT");
    }

    [Theory]
    [InlineData("vendor-missing")]
    [InlineData("vendor-id")]
    [InlineData("combination")]
    [InlineData("fund-missing")]
    [InlineData("fund-mismatch")]
    [InlineData("grant-missing")]
    [InlineData("grant-mismatch")]
    [InlineData("po-line")]
    public void Missing_or_mismatched_facts_are_refused_before_any_rule(string invalid)
    {
        var grantLine = SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 1000, budget: SubjectBuilder.Budget(100000, 0, 0));
        var s = Healthy();
        var d = s.Distributions[0];
        s = invalid switch
        {
            "vendor-missing" => s with { Transaction = s.Transaction with { Vendor = null! } },
            "vendor-id" => s with { Transaction = s.Transaction with { Vendor = s.Transaction.Vendor with { VendorId = Guid.Empty } } },
            "combination" => s with { Distributions = [d with { Combination = null! }] },
            "fund-missing" => s with { Distributions = [d with { Fund = null }] },
            "fund-mismatch" => s with { Distributions = [d with { Fund = SubjectBuilder.Grants701() }] },
            "grant-missing" => s with { Distributions = [grantLine with { Grant = null }] },
            "grant-mismatch" => s with { Distributions = [grantLine with { Grant = SubjectBuilder.Cops() with { Code = "G-OTHER" } }] },
            "po-line" => s with { Transaction = s.Transaction with { IsPoBacked = true } },
            _ => s
        };
        var r = Run(s);
        r.Overall.Should().Be(Severity.HardStop);
        r.Outcomes.Should().ContainSingle().Which.RuleId.Should().Be("VALIDATION_INPUT");
        r.Steps.Where(x => x.Step != ValidationStep.RequiredSegments).Should().OnlyContain(x => x.Status == StepExecutionStatus.Skipped);
    }

    [Fact]
    public void Invalid_evaluating_actor_is_rejected()
    {
        var r = Pipeline.Evaluate(Healthy(), Rules, EvaluationTrigger.Manual, default, At);
        r.Overall.Should().Be(Severity.HardStop);
    }

    [Theory]
    [InlineData("SEG_REQUIRED")]
    [InlineData("BUDGET_AVAILABILITY")]
    [InlineData("PO_LIQUIDATION")]
    [InlineData("APPROVAL_ROUTE")]
    public void Missing_mandatory_rule_fails_closed(string missing)
    {
        var r = Pipeline.Evaluate(Healthy(), DemoRules.All().Where(x => x.RuleId != missing).ToArray(), EvaluationTrigger.Post, SubjectBuilder.Approver, At);
        r.Overall.Should().Be(Severity.HardStop);
        r.Capabilities.CanPost.Should().BeFalse();
        r.Outcomes.Should().Contain(x => x.RuleId == "RULE_CONFIGURATION");
    }

    [Fact]
    public void Unknown_rule_fails_closed_even_after_an_early_hard_stop()
    {
        var rules = DemoRules.All().Append(DemoRules.Rule("UNKNOWN", ValidationStep.EncumbranceImpact, RuleLayer.Core, Severity.HardStop)).ToArray();
        var r = Pipeline.Evaluate(SubjectBuilder.Exercise(), rules, EvaluationTrigger.Post, SubjectBuilder.Approver, At);
        r.Outcomes.Should().Contain(x => x.RuleId == "RULE_CONFIGURATION");
    }

    [Fact]
    public void Record_preserves_full_rule_fingerprint_including_successes_and_route()
    {
        var r = Run(Healthy());
        r.RuleSetVersions.AppliedRules.Should().HaveCount(12);
        r.RuleSetVersions.AppliedRules.Should().Contain(x => x.RuleId == "APPROVAL_ROUTE");
        r.RuleSetVersions.AppliedRules.Should().Contain(x => x.RuleId == "VENDOR_ELIGIBLE");
        r.RuleFingerprint.Should().Be(Rules.Fingerprint);
        r.ApprovalCycleId.Should().Be(r.InputSnapshot.Transaction.ApprovalCycleId);
        r.EvaluatedAt.Should().Be(At);
        r.TransactionVersion.Should().Be(1);
    }

    [Theory]
    [InlineData("Posted", true, false, 0, true)]
    [InlineData("Approved", true, false, 0, false)]
    [InlineData("Posted", false, false, 0, false)]
    [InlineData("Posted", true, true, 0, false)]
    [InlineData("Posted", true, false, 1, false)]
    public void Payment_readiness_is_separate_and_uses_explicit_business_date(string status, bool active, bool hold, int dueOffset, bool expected)
    {
        var s = Healthy();
        s = s with { Transaction = s.Transaction with { Status = status, Vendor = s.Transaction.Vendor with { IsActive = active },
            PaymentHold = hold, DueDate = SubjectBuilder.June15.AddDays(dueOffset) } };
        var r = Run(s);
        r.ReadyForPaymentHandoff.Should().Be(expected);
        r.Capabilities.CanPay.Should().BeFalse();
        Run(s with { BusinessDate = null }).ReadyForPaymentHandoff.Should().BeFalse();
    }

    [Fact]
    public void Scoped_rules_are_not_omitted_or_applied_to_other_funds()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 5000, budget: SubjectBuilder.Budget(100000, 0, 0)))
            .With(SubjectBuilder.Distribution(2, "202-4000-53100", 5000, budget: SubjectBuilder.Budget(10000, 0, 0))).Build();
        var scoped = DemoRules.Rule("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant,
            Severity.Warning, new() { ["pct"] = "0.60" }, version: 2, scopeFund: "202");
        var r = Pipeline.Evaluate(s, DemoRules.All().Append(scoped).ToArray(), EvaluationTrigger.Manual, SubjectBuilder.Approver, At);
        r.Outcomes.Where(x => x.RuleId == "BUDGET_LOW_REMAINING").Should().ContainSingle(x => x.DistributionLine == 2 && x.RuleVersion == 2);
        r.RuleSetVersions.AppliedRules.Should().Contain(x => x.RuleId == "BUDGET_LOW_REMAINING" && x.Version == 1);
        r.RuleSetVersions.AppliedRules.Should().Contain(x => x.RuleId == "BUDGET_LOW_REMAINING" && x.Version == 2);
    }

    [Fact]
    public void Same_budget_distributions_are_aggregated()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 80000))
            .With(SubjectBuilder.Distribution(2, "701-6000-53100-G-COPS-26", 80000)).Build();
        var r = Run(s);
        r.Overall.Should().Be(Severity.HardStop);
        r.Outcomes.Single(x => x.RuleId == "BUDGET_AVAILABILITY").Computed["overage"].Should().Be("13,000.00");
    }
}
