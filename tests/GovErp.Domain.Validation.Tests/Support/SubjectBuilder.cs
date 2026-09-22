using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public sealed class SubjectBuilder
{
    public static readonly UserId Clerk = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly UserId Approver = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    public static readonly DateOnly June15 = new(2026, 6, 15);
    public static readonly PostingAccounts Accounts = new(new("2100"), new("2900"), new("5900"), new("0000"));
    public static FundSnapshot Grants701(FundRestriction restriction = FundRestriction.Allowed) =>
        new("701", "Grants Fund", FundKind.Governmental, BudgetControl.Hard, GrantRule.Required, restriction, true);
    public static FundSnapshot General101(FundRestriction restriction = FundRestriction.Allowed) =>
        new("101", "General Fund", FundKind.Governmental, BudgetControl.Soft, GrantRule.Forbidden, restriction, true);
    public static FundSnapshot Street202(FundRestriction restriction = FundRestriction.Allowed) =>
        new("202", "Street Fund", FundKind.Governmental, BudgetControl.Hard, GrantRule.Forbidden, restriction, true);
    public static FundSnapshot Water501() =>
        new("501", "Water Enterprise Fund", FundKind.Enterprise, BudgetControl.Soft, GrantRule.Forbidden, FundRestriction.Allowed, true);
    public static GrantSnapshot Cops(GrantEligibilityResult e = GrantEligibilityResult.Eligible) => new("G-COPS-26", true, e, "Active");
    public static BudgetSnapshot Budget(decimal amended, decimal actuals, decimal encumbered, decimal held = 0,
        decimal ownHeld = 0, int fiscalYear = 2026) =>
        new(true, Money.Of(amended), Money.Of(actuals), Money.Of(encumbered), Money.Of(held),
            Money.Of(amended - actuals - encumbered - held), Money.Of(ownHeld), fiscalYear);
    public static BudgetSnapshot ExerciseBudget() => Budget(375000, 132000, 96000);
    public static DistributionSnapshot Distribution(int lineNo, string account, decimal amount,
        FundSnapshot? fund = null, GrantSnapshot? grant = null, BudgetSnapshot? budget = null,
        CombinationSnapshot? combination = null, EncumbranceSnapshot? encumbrance = null, decimal allocatedLiquidation = 0)
    {
        var code = AccountCode.Parse(account);
        fund ??= code.Fund.Value switch { "701" => Grants701(), "101" => General101(), "202" => Street202(), "501" => Water501(), _ => null };
        grant ??= code.Grant is null ? null : Cops();
        return new(lineNo, code, Money.Of(amount), combination ?? new(true, true, "Active"), fund, grant,
            budget ?? ExerciseBudget(), encumbrance, Money.Of(allocatedLiquidation));
    }

    private readonly List<DistributionSnapshot> _distributions = [];
    private readonly List<ApproverRole> _approvals = [];
    private readonly List<ApprovalSnapshot> _detailedApprovals = [];
    private readonly List<OverrideSnapshot> _overrides = [];
    private Money? _total;
    private bool _isPoBacked, _isDuplicate, _paymentHold;
    private bool _periodOpen = true;
    private VendorSnapshot _vendor = new(Guid.NewGuid(), "Acme Consulting", false, true);
    private RuleSetVersions? _versionsAtApproval;
    private DateOnly _date = June15;
    private DateOnly _businessDate = June15;
    private DateOnly? _serviceDate, _postingDate, _dueDate;
    private int _contentVersion = 1;
    private Guid _approvalCycleId = Guid.NewGuid();
    private string _fingerprint = "";
    private string _status = "Draft";
    private Guid? _previousEvaluationId;
    private readonly List<RuleOutcome> _previousOutcomes = [];

    public SubjectBuilder With(DistributionSnapshot d) { _distributions.Add(d); return this; }
    public SubjectBuilder Total(decimal total) { _total = Money.Of(total); return this; }
    public SubjectBuilder PoBacked() { _isPoBacked = true; return this; }
    public SubjectBuilder Duplicate() { _isDuplicate = true; return this; }
    public SubjectBuilder PeriodClosed() { _periodOpen = false; return this; }
    public SubjectBuilder Vendor(bool debarred = false, bool sam = true, bool active = true)
    { _vendor = _vendor with { IsDebarred = debarred, SamRegistered = sam, IsActive = active }; return this; }
    public SubjectBuilder Approved(params ApproverRole[] roles) { _approvals.AddRange(roles); return this; }
    public SubjectBuilder Approval(ApprovalSnapshot approval) { _detailedApprovals.Add(approval); return this; }
    public SubjectBuilder Override(OverrideSnapshot value) { _overrides.Add(value); return this; }
    public SubjectBuilder Overridden(string ruleId, string reason = "justified")
    { _overrides.Add(new(ruleId, Approver, reason)); return this; }
    public SubjectBuilder VersionsAtApproval(RuleSetVersions v) { _versionsAtApproval = v; return this; }
    public SubjectBuilder Dated(DateOnly d) { _date = d; return this; }
    public SubjectBuilder ContentVersion(int version) { _contentVersion = version; return this; }
    public SubjectBuilder ApprovalCycle(Guid cycle, string fingerprint) { _approvalCycleId = cycle; _fingerprint = fingerprint; return this; }
    public SubjectBuilder Status(string status) { _status = status; return this; }
    public SubjectBuilder Dates(DateOnly serviceDate, DateOnly postingDate, DateOnly dueDate)
    { _serviceDate = serviceDate; _postingDate = postingDate; _dueDate = dueDate; return this; }
    public SubjectBuilder BusinessDate(DateOnly date) { _businessDate = date; return this; }
    public SubjectBuilder PaymentHold(bool held = true) { _paymentHold = held; return this; }
    public SubjectBuilder PreviousEvaluation(Guid id, params RuleOutcome[] outcomes)
    { _previousEvaluationId = id; _previousOutcomes.Clear(); _previousOutcomes.AddRange(outcomes); return this; }

    public ValidationSubject Build()
    {
        var distributions = _distributions.Count == 0
            ? new[] { Distribution(1, "701-6000-53100-G-COPS-26", 160000) } : _distributions.ToArray();
        var total = _total ?? distributions.Aggregate(Money.Zero, (sum, d) => sum + d.Amount);
        var tx = new TransactionSnapshot("INV-V-7781", _contentVersion, "AP_INVOICE", _date, total, _vendor,
            _isPoBacked, _isDuplicate, Clerk, _approvalCycleId, _fingerprint, _status,
            _serviceDate ?? _date, _postingDate ?? _date, _dueDate ?? _date, _paymentHold);
        return new(tx, distributions, _approvals, _overrides, _periodOpen, _versionsAtApproval, Accounts,
            _detailedApprovals, _businessDate, _previousOutcomes, _previousEvaluationId);
    }
    public static ValidationSubject Exercise() => new SubjectBuilder().Build();
}
