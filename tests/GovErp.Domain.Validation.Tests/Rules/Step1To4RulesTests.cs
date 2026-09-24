using GovErp.Domain.Validation.DomainServices.Rules;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests.Rules;

public class Step1To4RulesTests
{
    // Missing or mismatched facts (fund, grant, combination, vendor) are refused by the pipeline before any rule runs:
    // PipelineScenarioTests.Missing_or_mismatched_facts_are_refused_before_any_rule.

    [Fact]
    public void Inactive_vendor_is_hard_stop()
    {
        var s = SubjectBuilder.Exercise();
        s = s with { Transaction = s.Transaction with { Vendor = s.Transaction.Vendor with { IsActive = false } } };
        new VendorEligibleRule().Evaluate(s, Def("VENDOR_ELIGIBLE"))
            .Should().ContainSingle(o => o.Severity == Severity.HardStop && o.DistributionLine == null);
    }

    [Theory]
    [InlineData(true, "2026-05-01")]
    [InlineData(false, "2026-06-15")]
    public void Grant_period_evidence_uses_service_date_or_invoice_date(bool serviceDate, string expected)
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 1m,
            grant: SubjectBuilder.Cops(GrantEligibilityResult.OutsidePeriod))).Build();
        s = s with { Transaction = s.Transaction with
        { Date = new DateOnly(2026, 6, 15), ServiceDate = serviceDate ? new DateOnly(2026, 5, 1) : null } };
        var outcome = new GrantEligibleRule().Evaluate(s, Def("GRANT_ELIGIBLE")).Single();
        outcome.Inputs["date"].Should().Be(expected);
        outcome.ReasonCode.Should().Be("GRANT_ELIGIBLE.OUTSIDE_PERIOD");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Nonpositive_procurement_threshold_is_rejected(string threshold)
    {
        var definition = new Entities.RuleDefinition("PROCUREMENT_THRESHOLD", 1, ValidationStep.TransactionPurpose,
            RuleLayer.State, Severity.SoftStop, new Dictionary<string, string> { ["threshold"] = threshold }, [],
            new DateOnly(2026, 1, 1), null, "Threshold", "Correct configuration");
        var evaluate = () => new ProcurementThresholdRule().Evaluate(SubjectBuilder.Exercise(), definition);
        evaluate.Should().Throw<Exceptions.ValidationException>();
    }

    [Fact]
    public void Object_restriction_is_reported_for_the_correct_line()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 1m))
            .With(SubjectBuilder.Distribution(2, "202-4000-53100", 1m,
                fund: SubjectBuilder.Street202(FundRestriction.ObjectNotAllowed))).Build();
        new FundDeptObjectAllowedRule().Evaluate(s, Def("FUND_DEPT_OBJECT_ALLOWED"))
            .Should().ContainSingle(o => o.DistributionLine == 2 && o.Computed["restriction"] == "ObjectNotAllowed");
    }

    [Fact]
    public void Inactive_fund_fails_closed()
    {
        var d = SubjectBuilder.Distribution(1, "101-6000-53100", 1m,
            fund: SubjectBuilder.General101() with { IsActive = false });
        new FundDeptObjectAllowedRule().Evaluate(new SubjectBuilder().With(d).Build(), Def("FUND_DEPT_OBJECT_ALLOWED"))
            .Should().ContainSingle(o => o.Severity == Severity.HardStop);
    }

    [Theory]
    [InlineData(24999.99, false)]
    [InlineData(25000, true)]
    [InlineData(25000.01, true)]
    public void Procurement_threshold_is_inclusive(decimal total, bool fails)
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", total)).Build();
        new ProcurementThresholdRule().Evaluate(s, Def("PROCUREMENT_THRESHOLD")).Count.Should().Be(fails ? 1 : 0);
    }

    [Fact]
    public void Procurement_uses_configured_threshold_severity_and_metadata()
    {
        var definition = new Entities.RuleDefinition("PROCUREMENT_THRESHOLD", 3, ValidationStep.TransactionPurpose,
            RuleLayer.Tenant, Severity.Warning, new Dictionary<string, string> { ["threshold"] = "160000" },
            [ApproverRole.FinanceDirector], new DateOnly(2026, 1, 1), null, "Threshold reached", "Document exception");
        var outcome = new ProcurementThresholdRule().Evaluate(SubjectBuilder.Exercise(), definition).Single();
        outcome.Severity.Should().Be(Severity.Warning);
        outcome.RuleVersion.Should().Be(3);
        outcome.Layer.Should().Be(RuleLayer.Tenant);
        outcome.Step.Should().Be(ValidationStep.TransactionPurpose);
        outcome.Resolution.Should().Be("Document exception");
        outcome.Inputs["threshold"].Should().Be("160,000.00");
        outcome.OverridableBy.Should().BeEmpty();
        var below = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 159999m)).Build();
        new ProcurementThresholdRule().Evaluate(below, definition).Should().BeEmpty();
    }

    [Fact]
    public void Rule_evidence_cannot_be_mutated()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(2, "701-6000-53100", 1234.56m)).Build();
        var outcome = new SegRequiredRule().Evaluate(s, Def("SEG_REQUIRED")).Single();
        outcome.Inputs["account"].Should().Be("701-6000-53100");
        outcome.Inputs["amount"].Should().Be("1,234.56");
        outcome.Computed["missingSegment"].Should().Be("Grant");
        var mutateInputs = () => ((IDictionary<string, string>)outcome.Inputs)["amount"] = "0";
        var mutateComputed = () => ((IDictionary<string, string>)outcome.Computed)["missingSegment"] = "";
        mutateInputs.Should().Throw<NotSupportedException>();
        mutateComputed.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Valid_static_facts_produce_no_failures()
    {
        var s = SubjectBuilder.Exercise();
        new SegGrantForbiddenRule().Evaluate(s, Def("SEG_GRANT_FORBIDDEN")).Should().BeEmpty();
        new CoaCombinationActiveRule().Evaluate(s, Def("COA_COMBINATION_ACTIVE")).Should().BeEmpty();
        new FundDeptObjectAllowedRule().Evaluate(s, Def("FUND_DEPT_OBJECT_ALLOWED")).Should().BeEmpty();
        new GrantEligibleRule().Evaluate(s, Def("GRANT_ELIGIBLE")).Should().BeEmpty();
        new VendorEligibleRule().Evaluate(s, Def("VENDOR_ELIGIBLE")).Should().BeEmpty();
        new InvoiceDuplicateRule().Evaluate(s, Def("INVOICE_DUPLICATE")).Should().BeEmpty();
    }

    private static Entities.RuleDefinition Def(string id) => new(id, 1,
        id.StartsWith("SEG_", StringComparison.Ordinal) ? ValidationStep.RequiredSegments :
        id == "COA_COMBINATION_ACTIVE" ? ValidationStep.ValidCombination :
        id is "FUND_DEPT_OBJECT_ALLOWED" or "GRANT_ELIGIBLE" ? ValidationStep.FundAndGrantRestrictions : ValidationStep.TransactionPurpose,
        id == "PROCUREMENT_THRESHOLD" ? RuleLayer.State : id is "GRANT_ELIGIBLE" or "VENDOR_ELIGIBLE" ? RuleLayer.Federal :
        id == "FUND_DEPT_OBJECT_ALLOWED" ? RuleLayer.Tenant : RuleLayer.Core,
        id == "PROCUREMENT_THRESHOLD" ? Severity.SoftStop : Severity.HardStop,
        id == "PROCUREMENT_THRESHOLD" ? new Dictionary<string, string> { ["threshold"] = "25000" } : new Dictionary<string, string>(),
        id == "PROCUREMENT_THRESHOLD" ? [ApproverRole.FinanceDirector] : [],
        new DateOnly(2026, 1, 1), null, "Validation failed", "Correct the source facts.");

    [Fact]
    public void SegRequired_701_without_grant_is_hard_stop()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100", 1000m, fund: SubjectBuilder.Grants701(), grant: null)).Build();
        var o = new SegRequiredRule().Evaluate(s, Def("SEG_REQUIRED"));
        o.Should().ContainSingle(x => x.Severity == Severity.HardStop && x.DistributionLine == 1);
        o[0].Inputs["grantPolicy"].Should().Be("Required");
    }

    [Fact]
    public void SegRequired_701_with_grant_passes() =>
        new SegRequiredRule().Evaluate(SubjectBuilder.Exercise(), Def("SEG_REQUIRED")).Should().BeEmpty();

    [Fact]
    public void SegGrantForbidden_101_with_grant_is_hard_stop()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100-G-COPS-26", 1000m, fund: SubjectBuilder.General101())).Build();
        new SegGrantForbiddenRule().Evaluate(s, Def("SEG_GRANT_FORBIDDEN")).Should().ContainSingle(x => x.Severity == Severity.HardStop);
    }

    [Fact]
    public void CoaCombination_missing_and_inactive_are_hard_stop()
    {
        var missing = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 1m,
            combination: new CombinationSnapshot(false, false, "Missing"))).Build();
        var inactive = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-FEMA-24", 1m,
            combination: new CombinationSnapshot(true, false, "Inactive"))).Build();
        new CoaCombinationActiveRule().Evaluate(missing, Def("COA_COMBINATION_ACTIVE")).Should().ContainSingle(x => x.ReasonCode == "COA_COMBINATION_ACTIVE.COMBINATION_UNKNOWN");
        new CoaCombinationActiveRule().Evaluate(inactive, Def("COA_COMBINATION_ACTIVE")).Should().ContainSingle(x => x.ReasonCode == "COA_COMBINATION_ACTIVE.COMBINATION_INACTIVE" && x.Inputs["status"] == "Inactive");
    }

    [Fact]
    public void FundDeptObject_restriction_is_hard_stop()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "202-3000-53100", 1m,
            fund: SubjectBuilder.Street202(FundRestriction.DepartmentNotAllowed))).Build();
        new FundDeptObjectAllowedRule().Evaluate(s, Def("FUND_DEPT_OBJECT_ALLOWED"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Computed["restriction"] == "DepartmentNotAllowed");
    }

    [Theory]
    [InlineData(GrantEligibilityResult.GrantNotActive)]
    [InlineData(GrantEligibilityResult.OutsidePeriod)]
    [InlineData(GrantEligibilityResult.DepartmentNotAllowed)]
    [InlineData(GrantEligibilityResult.ObjectNotAllowed)]
    public void GrantEligible_failures_are_hard_stop(GrantEligibilityResult e)
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-4000-53100-G-COPS-26", 1m, grant: SubjectBuilder.Cops(e))).Build();
        new GrantEligibleRule().Evaluate(s, Def("GRANT_ELIGIBLE")).Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Computed["eligibility"] == e.ToString());
    }

    [Fact]
    public void GrantEligible_skips_distributions_without_grant()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 1m)).Build();
        new GrantEligibleRule().Evaluate(s, Def("GRANT_ELIGIBLE")).Should().BeEmpty();
    }

    [Fact]
    public void VendorEligible_debarred_is_hard_stop_document_level()
    {
        var s = new SubjectBuilder().Vendor(debarred: true).Build();
        new VendorEligibleRule().Evaluate(s, Def("VENDOR_ELIGIBLE")).Should().ContainSingle(x => x.Severity == Severity.HardStop && x.DistributionLine == null);
    }

    [Fact]
    public void VendorEligible_federal_grant_requires_sam()
    {
        var s = new SubjectBuilder().Vendor(sam: false).Build();   // 701 + G-COPS-26 (federal)
        new VendorEligibleRule().Evaluate(s, Def("VENDOR_ELIGIBLE")).Should().ContainSingle(x => x.ReasonCode == "VENDOR_ELIGIBLE.SAM_REGISTRATION_REQUIRED");
        var nonFederal = new SubjectBuilder().Vendor(sam: false).With(SubjectBuilder.Distribution(1, "101-6000-53100", 1m)).Build();
        new VendorEligibleRule().Evaluate(nonFederal, Def("VENDOR_ELIGIBLE")).Should().BeEmpty();
    }

    [Fact]
    public void ProcurementThreshold_non_po_at_or_above_threshold_is_soft_stop()
    {
        var o = new ProcurementThresholdRule().Evaluate(SubjectBuilder.Exercise(), Def("PROCUREMENT_THRESHOLD"));
        o.Should().ContainSingle(x => x.Severity == Severity.SoftStop && x.OverridableBy.Contains(ApproverRole.FinanceDirector));
        o[0].Inputs["threshold"].Should().Be("25,000.00");
    }

    [Fact]
    public void ProcurementThreshold_skips_po_backed_and_small()
    {
        new ProcurementThresholdRule().Evaluate(new SubjectBuilder().PoBacked().Build(), Def("PROCUREMENT_THRESHOLD")).Should().BeEmpty();
        new ProcurementThresholdRule().Evaluate(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 24_999m)).Build(),
            Def("PROCUREMENT_THRESHOLD")).Should().BeEmpty();
    }

    [Fact]
    public void InvoiceDuplicate_is_hard_stop() =>
        new InvoiceDuplicateRule().Evaluate(new SubjectBuilder().Duplicate().Build(), Def("INVOICE_DUPLICATE"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop);
}

