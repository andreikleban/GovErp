using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class ApprovalAndPostingTests
{
    private static readonly DateOnly BusinessDate = new(2026, 6, 15);
    private static readonly EffectiveRuleSet Rules = RuleResolution.Resolve([], BusinessDate);
    private static readonly UserId Author = new(Guid.NewGuid());
    private static readonly UserId Approver = new(Guid.NewGuid());

    [Theory]
    [InlineData(49999.99, false)]
    [InlineData(50000, true)]
    public void Route_requires_each_department_grants_and_inclusive_director_threshold(decimal total, bool director)
    {
        var s = Subject(total) with { Distributions = [Distribution(1, total - 100), Distribution(2, 100, "701-3000-53100-G-COPS-26")] };
        var route = ApprovalRouteResolver.Build(s, [], Rules);
        route.Where(r => r.Role == ApproverRole.DepartmentHead).Select(r => r.Department)
            .Should().BeEquivalentTo("6000", "3000");
        route.Should().Contain(r => r.Role == ApproverRole.GrantsManager);
        route.Any(r => r.Role == ApproverRole.FinanceDirector).Should().Be(director);
    }

    [Theory]
    [InlineData("department")]
    [InlineData("role")]
    [InlineData("author")]
    [InlineData("content")]
    [InlineData("cycle")]
    [InlineData("fingerprint")]
    [InlineData("rejected")]
    [InlineData("evaluation")]
    [InlineData("legacy")]
    public void Invalid_or_legacy_approval_cannot_satisfy_route(string fault)
    {
        var s = Subject();
        var approval = Approval(s);
        approval = fault switch
        {
            "department" => approval with { Department = new("3000") },
            "role" => approval with { Role = ApproverRole.BudgetOfficer },
            "author" => approval with { UserId = Author },
            "content" => approval with { ContentVersion = 2 },
            "cycle" => approval with { ApprovalCycleId = Guid.NewGuid() },
            "fingerprint" => approval with { RuleFingerprint = "old" },
            "rejected" => approval with { IsApproved = false },
            "evaluation" => approval with { EvaluationId = Guid.Empty },
            _ => approval,
        };
        s = s with { DetailedApprovals = fault == "legacy" ? [] : [approval], ApprovalsSoFar = [ApproverRole.DepartmentHead] };
        ApprovalRouteResolver.Build(s, [], Rules).Should().ContainSingle(r => !r.IsSatisfied);
    }

    [Fact]
    public void Current_approval_satisfies_only_its_department()
    {
        var s = Subject() with { Distributions = [Distribution(1, 50), Distribution(2, 50, "101-3000-53100")] };
        s = s with { DetailedApprovals = [Approval(s)] };
        var route = ApprovalRouteResolver.Build(s, [], Rules);
        route.Single(r => r.Department == "6000").IsSatisfied.Should().BeTrue();
        route.Single(r => r.Department == "3000").IsSatisfied.Should().BeFalse();
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    [InlineData("Rejected")]
    [InlineData("Posted")]
    public void Only_approved_status_can_post(string status)
    {
        var s = Approved();
        Check(s with { Transaction = s.Transaction with { Status = status } }).Passed.Should().BeFalse();
    }

    [Fact]
    public void Approved_current_warning_can_post_but_is_not_payment_ready()
    {
        var s = Approved();
        Check(s, Severity.Warning).Passed.Should().BeTrue();
        PostingEligibility.ReadyForPaymentHandoff(s, BusinessDate).Should().BeFalse();
    }

    [Theory]
    [InlineData(Severity.SoftStop)]
    [InlineData(Severity.HardStop)]
    public void Unresolved_stop_blocks_posting(Severity severity) => Check(Approved(), severity).Passed.Should().BeFalse();

    [Fact]
    public void Missing_checks_closed_period_and_stale_fingerprint_fail_closed()
    {
        var s = Approved();
        Check(s with { PeriodIsOpen = false }).Passed.Should().BeFalse();
        Check(s with { Transaction = s.Transaction with { RuleFingerprint = "old" } }).Passed.Should().BeFalse();
        PostingEligibility.Check(s, null, ApprovalRouteResolver.Build(s, [], Rules), PostingPreviewBuilder.Build(s), Rules).Passed.Should().BeFalse();
        PostingEligibility.Check(s, Severity.Allowed, null, PostingPreviewBuilder.Build(s), Rules).Passed.Should().BeFalse();
        PostingEligibility.Check(s, Severity.Allowed, ApprovalRouteResolver.Build(s, [], Rules), null, Rules).Passed.Should().BeFalse();
    }

    [Fact]
    public void Reapproval_with_current_fingerprint_allows_post_despite_legacy_versions()
    {
        var s = Approved() with { VersionsAtLastApproval = new("old", 99, 99, 99, 99) };
        Check(s).Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData("Posted", true, false, 0, true)]
    [InlineData("Posted", true, false, -1, true)]
    [InlineData("Posted", true, false, 1, false)]
    [InlineData("Approved", true, false, 0, false)]
    [InlineData("Posted", false, false, 0, false)]
    [InlineData("Posted", true, true, 0, false)]
    public void Payment_handoff_uses_explicit_business_date(string status, bool active, bool hold, int dueDays, bool ready)
    {
        var s = Subject();
        s = s with { Transaction = s.Transaction with { Status = status, Vendor = s.Transaction.Vendor with { IsActive = active }, PaymentHold = hold, DueDate = BusinessDate.AddDays(dueDays) } };
        PostingEligibility.ReadyForPaymentHandoff(s, BusinessDate).Should().Be(ready);
    }

    [Fact]
    public void Missing_due_date_is_not_payment_ready()
    {
        var s = Subject();
        PostingEligibility.ReadyForPaymentHandoff(s with { Transaction = s.Transaction with { Status = "Posted", DueDate = null } }, BusinessDate).Should().BeFalse();
    }

    [Fact]
    public void Financial_preview_groups_ap_by_fund_and_preserves_expense_accounts()
    {
        var s = Subject() with { Distributions = [Distribution(1, 40), Distribution(2, 60), Distribution(3, 10, "501-5000-53100")] };
        var preview = PostingPreviewBuilder.Build(s);
        preview.Lines.Where(l => l.Account.Object.Value == "2100").Should().HaveCount(2);
        preview.Lines.Single(l => l.Account.Fund.Value == "501" && l.Debit > Money.Zero).Description.Should().Contain("Expense");
        preview.Lines.First(l => l.Debit > Money.Zero).Description.Should().Contain("Expenditure");
        PostingPreviewBuilder.IsBalancedPerFund(preview.Lines).Should().BeTrue();
    }

    [Fact]
    public void Shared_po_liquidation_is_not_counted_twice()
    {
        var po = new EncumbranceSnapshot("PO-1/1", Money.Of(96), true, Money.Of(100));
        var s = Subject() with { Distributions = [Distribution(1, 50) with { Encumbrance = po }, Distribution(2, 50) with { Encumbrance = po }] };
        s = s with { Transaction = s.Transaction with { IsPoBacked = true } };
        var preview = PostingPreviewBuilder.Build(s);
        preview.Lines.Where(l => l.Family == "Budgetary").Sum(l => l.Debit.Amount).Should().Be(96);
        preview.Lines.Where(l => l.Family == "Financial").Sum(l => l.Debit.Amount).Should().Be(100);
        PostingPreviewBuilder.IsBalancedPerFund(preview.Lines).Should().BeTrue();
    }

    [Fact]
    public void Closed_po_cannot_produce_a_false_preview()
    {
        var s = Subject() with { Distributions = [Distribution(1, 100) with { Encumbrance = new("PO-1/1", Money.Of(100), false, Money.Of(100)) }] };
        s = s with { Transaction = s.Transaction with { IsPoBacked = true } };
        var preview = PostingPreviewBuilder.Build(s);
        preview.Lines.Should().BeEmpty();
        preview.Failures.Should().NotBeEmpty();
    }

    [Fact]
    public void Balance_must_hold_for_each_fund_and_family()
    {
        PostingPreviewLine[] crossFamily = [new(AccountCode.Parse("101-6000-53100"), "Financial", Money.Of(10), Money.Zero, "x"), new(AccountCode.Parse("101-0000-2900"), "Budgetary", Money.Zero, Money.Of(10), "x")];
        PostingPreviewBuilder.IsBalancedPerFund(crossFamily).Should().BeFalse();
        PostingPreviewBuilder.IsBalancedPerFund([crossFamily[0], crossFamily[1] with { Family = "Financial", Account = AccountCode.Parse("202-0000-2100") }]).Should().BeFalse();
        PostingPreviewBuilder.IsBalancedPerFund([]).Should().BeFalse();
    }

    [Fact]
    public void Caller_supplied_routes_cannot_bypass_detailed_approval_evidence()
    {
        var s = Subject();
        s = s with { Transaction = s.Transaction with { Status = "Approved" } };
        var preview = PostingPreviewBuilder.Build(s);
        PostingEligibility.Check(s, Severity.Allowed, [], preview, Rules).Passed.Should().BeFalse();
        PostingEligibility.Check(s, Severity.Allowed,
            [new(ApproverRole.DepartmentHead, "6000", "forged", true)], preview, Rules).Passed.Should().BeFalse();
    }

    [Fact]
    public void Balanced_preview_for_another_amount_cannot_post()
    {
        var s = Approved();
        var unrelated = PostingPreviewBuilder.Build(Subject(1));
        PostingEligibility.Check(s, Severity.Allowed, ApprovalRouteResolver.Build(s, [], Rules), unrelated, Rules)
            .Passed.Should().BeFalse();
    }

    [Fact]
    public void Missing_required_department_cannot_be_hidden_by_a_partial_route()
    {
        var s = Approved() with { Distributions = [Distribution(1, 50), Distribution(2, 50, "101-3000-53100")] };
        PostingEligibility.Check(s, Severity.Allowed,
            [new(ApproverRole.DepartmentHead, "6000", "partial", true)], PostingPreviewBuilder.Build(s), Rules)
            .Passed.Should().BeFalse();
    }
    [Fact]
    public void Applicable_scoped_route_uses_strongest_director_threshold()
    {
        var s = Subject(40000) with { Distributions = [Distribution(1, 20000), Distribution(2, 20000, "202-3000-53100")] };
        Entities.RuleDefinition Route(string fund, string threshold) => new("APPROVAL_ROUTE", 1,
            ValidationStep.ApprovalRequirements, RuleLayer.Core, null,
            new Dictionary<string, string> { ["finance_director_threshold"] = threshold }, [], BusinessDate, null,
            "Approval", "Approve", scopeFund: fund);
        var rules = RuleResolution.ResolveForSubject([Route("101", "50000"), Route("202", "30000")], s);
        ApprovalRouteResolver.Build(s, [], rules).Should().Contain(r => r.Role == ApproverRole.FinanceDirector);
    }

    [Fact]
    public void Unresolved_soft_stop_adds_overrider_without_authorizing_post()
    {
        var s = Approved();
        var definition = new Entities.RuleDefinition("PROCUREMENT_THRESHOLD", 1, ValidationStep.TransactionPurpose,
            RuleLayer.Core, Severity.SoftStop, new Dictionary<string, string>(), [ApproverRole.FinanceDirector], BusinessDate, null,
            "Approval required", "Override");
        var outcome = RuleOutcome.From(definition, Severity.SoftStop, null, new Dictionary<string, string>(), new Dictionary<string, string>());
        var route = ApprovalRouteResolver.Build(s, [outcome], Rules);
        route.Should().Contain(r => r.Role == ApproverRole.FinanceDirector && r.Reason.Contains("PROCUREMENT_THRESHOLD"));
        PostingEligibility.Check(s, Severity.SoftStop, route, PostingPreviewBuilder.Build(s), Rules).Passed.Should().BeFalse();
    }
    private static PostingCheck Check(ValidationSubject s, Severity severity = Severity.Allowed) =>
        PostingEligibility.Check(s, severity, ApprovalRouteResolver.Build(s, [], Rules), PostingPreviewBuilder.Build(s), Rules);

    private static ValidationSubject Approved()
    {
        var s = Subject();
        return s with { Transaction = s.Transaction with { Status = "Approved" }, DetailedApprovals = [Approval(s)] };
    }

    private static ApprovalSnapshot Approval(ValidationSubject s) => new(ApproverRole.DepartmentHead, Approver, new("6000"),
        s.Transaction.ContentVersion, s.Transaction.ApprovalCycleId, Rules.Fingerprint, Guid.NewGuid());

    private static ValidationSubject Subject(decimal total = 100) => new(
        new("INV-1", 1, "Invoice", BusinessDate, Money.Of(total), new(Guid.NewGuid(), "Vendor", false, true), false, false, Author,
            Guid.NewGuid(), Rules.Fingerprint, "Draft", BusinessDate, BusinessDate, BusinessDate),
        [Distribution(1, total)], [], [], true, Rules.Versions, new(new("2100"), new("2900"), new("5900"), new("0000")));

    private static DistributionSnapshot Distribution(int line, decimal amount, string account = "101-6000-53100")
    {
        var code = AccountCode.Parse(account);
        return new(line, code, Money.Of(amount), new(true, true, "Active"),
            new(code.Fund.Value, "Fund", code.Fund.Value == "501" ? FundKind.Enterprise : FundKind.Governmental, BudgetControl.Hard, GrantRule.Optional, FundRestriction.Allowed, true),
            code.Grant is null ? null : new(code.Grant.Value, true, GrantEligibilityResult.Eligible, "Active"),
            new(true, Money.Of(1000000), Money.Zero, Money.Zero, Money.Zero, Money.Of(1000000)), null);
    }
}



