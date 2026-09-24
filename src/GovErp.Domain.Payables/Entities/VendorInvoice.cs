using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// A vendor invoice: its lines, approval cycle, evaluations and posting.
/// </summary>
public sealed class VendorInvoice
{
    private readonly List<InvoiceDistribution> _distributions = [];
    private readonly List<InvoiceApproval> _approvals = [];
    private readonly List<InvoiceOverride> _overrides = [];
    private readonly List<InvoiceWithdrawal> _withdrawals = [];
    private IReadOnlyList<ApprovalRequirement> _requirements = Array.Empty<ApprovalRequirement>();
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Number { get; private set; }
    public string NormalizedInvoiceNumber => NormalizeNumber(Number);
    /// <summary>
    /// Comparison rule for vendor invoice numbers in the duplicate check (also before the invoice is created).
    /// </summary>
    public static string NormalizeNumber(string number) => number.Trim().ToUpperInvariant();
    /// <summary>
    /// The document's AP registration number (SourceRef in the journal, TransactionRef in evaluations). Immutable.
    /// </summary>
    public string Reference { get; private set; }
    public Guid VendorId { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public Money Total { get; private set; }
    public string? PoRef { get; private set; }
    public bool IsPoBacked => PoRef is not null;
    public UserId CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    /// <summary>When the payment journal entry was recorded. The status stays Posted.</summary>
    public DateTimeOffset? PaidAt { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;
    public int ContentVersion { get; private set; } = 1;
    public Guid? ApprovalCycleId { get; private set; }
    public Guid? LastEvaluationRef { get; private set; }
    public string? RuleFingerprint { get; private set; }
    public bool PaymentHold { get; private set; }
    /// <summary>
    /// Grows on any change, including owned collections: storage always updates the root and checks the concurrency version.
    /// </summary>
    public long ChangeStamp { get; private set; }
    public IReadOnlyList<InvoiceDistribution> Distributions => _distributions.AsReadOnly();
    public IReadOnlyList<InvoiceApproval> Approvals => _approvals.AsReadOnly();
    public IReadOnlyList<InvoiceOverride> Overrides => _overrides.AsReadOnly();
    public IReadOnlyList<InvoiceWithdrawal> Withdrawals => _withdrawals.AsReadOnly();
    public IReadOnlyList<Guid> ReservationRefs { get; private set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> EncumbranceClaimRefs { get; private set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> PoBillingClaimRefs { get; private set; } = Array.Empty<Guid>();
    public Money DistributedTotal => _distributions.Aggregate(Money.Zero, (s, d) => s + d.Amount);

    private VendorInvoice() { Number = null!; Reference = null!; }

    public VendorInvoice(string number, Guid vendorId, DateOnly invoiceDate, DateOnly serviceDate, DateOnly postingDate,
        DateOnly dueDate, Money total, string? poRef, UserId createdBy, DateTimeOffset createdAt, string? reference = null)
    {
        CheckHeader(number, vendorId, total, poRef);
        if (reference is not null && string.IsNullOrWhiteSpace(reference)) throw new PayablesException(PayablesErrors.BlankReference);
        CheckDates(invoiceDate, dueDate);
        CheckUser(createdBy);
        Number = number; VendorId = vendorId; InvoiceDate = invoiceDate; ServiceDate = serviceDate;
        PostingDate = postingDate; DueDate = dueDate; Total = total; PoRef = poRef;
        CreatedBy = createdBy; CreatedAt = createdAt;
        Reference = reference ?? $"INV-{Id}";
    }

    public void UpdateHeader(string number, Guid vendorId, DateOnly invoiceDate, DateOnly serviceDate, DateOnly postingDate,
        DateOnly dueDate, Money total, string? poRef)
    {
        Require(InvoiceStatus.Draft);
        CheckHeader(number, vendorId, total, poRef);
        CheckDates(invoiceDate, dueDate);
        if ((Number, VendorId, InvoiceDate, ServiceDate, PostingDate, DueDate, Total, PoRef) ==
            (number, vendorId, invoiceDate, serviceDate, postingDate, dueDate, total, poRef)) return;
        Number = number; VendorId = vendorId; InvoiceDate = invoiceDate; ServiceDate = serviceDate;
        PostingDate = postingDate; DueDate = dueDate; Total = total; PoRef = poRef;
        ContentVersion++; ChangeStamp++;
    }

    public void AddDistribution(AccountCode account, Money amount, int? poLineNo)
    {
        Require(InvoiceStatus.Draft);
        ArgumentNullException.ThrowIfNull(account);
        if (amount <= Money.Zero || poLineNo is <= 0) throw new PayablesException(PayablesErrors.DistributionNotPositive);
        _ = DistributedTotal + amount;
        _distributions.Add(new(_distributions.Count + 1, account, amount, poLineNo));
        ContentVersion++; ChangeStamp++;
    }

    /// <summary>
    /// Replaces the draft's lines with the given ones; identical lines change nothing, not even the content version.
    /// </summary>
    public void ReplaceDistributions(IReadOnlyList<(AccountCode Account, Money Amount, int? PoLineNo)> lines)
    {
        Require(InvoiceStatus.Draft);
        ArgumentNullException.ThrowIfNull(lines);
        if (_distributions.Select(d => (d.Account, d.Amount, d.PoLineNo)).SequenceEqual(lines))
        {
            return;
        }

        for (var lineNo = _distributions.Count; lineNo >= 1; lineNo--)
        {
            RemoveDistribution(lineNo);
        }

        foreach (var (account, amount, poLineNo) in lines)
        {
            AddDistribution(account, amount, poLineNo);
        }
    }

    public void RemoveDistribution(int lineNo)
    {
        Require(InvoiceStatus.Draft);
        var index = _distributions.FindIndex(d => d.LineNo == lineNo);
        if (index < 0) throw new PayablesException(PayablesErrors.DistributionNotFound, ("line", lineNo));
        _distributions.RemoveAt(index);
        for (var i = index; i < _distributions.Count; i++) _distributions[i] = _distributions[i] with { LineNo = i + 1 };
        ContentVersion++; ChangeStamp++;
    }

    public void Submit(Guid evaluationRef, string ruleFingerprint, IReadOnlyList<ApprovalRequirement> requirements,
        IReadOnlyList<Guid> reservations, IReadOnlyList<Guid>? encumbranceClaims = null, IReadOnlyList<Guid>? billingClaims = null)
    {
        Require(InvoiceStatus.Draft);
        CheckEvaluation(evaluationRef, ruleFingerprint);
        var route = CheckRoute(requirements);
        var held = CopyIds(reservations);
        var enc = CopyIds(encumbranceClaims ?? []);
        var bill = CopyIds(billingClaims ?? []);
        if (_distributions.Count == 0 || DistributedTotal != Total) throw new PayablesException(PayablesErrors.DistributionsNotEqualTotal, ("distributed", DistributedTotal), ("total", Total));
        ReservationRefs = held; EncumbranceClaimRefs = enc; PoBillingClaimRefs = bill;
        _requirements = route;
        ApprovalCycleId = Guid.NewGuid(); LastEvaluationRef = evaluationRef; RuleFingerprint = ruleFingerprint;
        Status = InvoiceStatus.Submitted; ChangeStamp++;
    }

    public void RecordApproval(ApproverRole role, DepartmentCode? department, UserId user, Guid evaluationRef, DateTimeOffset at)
    {
        Require(InvoiceStatus.Submitted);
        CheckApprover(role, department, user);
        if (evaluationRef != LastEvaluationRef) throw new PayablesException(PayablesErrors.StaleEvaluation);
        if (CurrentApprovals().Any(a => a.Role == role && a.Department == department)) throw new PayablesException(PayablesErrors.AlreadyApproved, ("role", role));
        _approvals.Add(new(role, department, user, ApprovalDecision.Approved, evaluationRef,
            ContentVersion, ApprovalCycleId!.Value, RuleFingerprint!, null, at));
        ChangeStamp++;
    }

    public void MarkApproved()
    {
        Require(InvoiceStatus.Submitted);
        if (!_requirements.All(r => CurrentApprovals().Any(a => a.Role == r.Role && a.Department == r.Department)))
            throw new PayablesException(PayablesErrors.ApprovalsMissing);
        Status = InvoiceStatus.Approved; ChangeStamp++;
    }

    public void Override(OverrideTarget target, ApproverRole role, UserId user, string reason, DateTimeOffset at)
    {
        RequireActive(); CheckUser(user); RequireReason(reason);
        if (!Enum.IsDefined(role)) throw new PayablesException(PayablesErrors.OverrideRoleInvalid);
        ArgumentNullException.ThrowIfNull(target);
        if (user == CreatedBy || target.EvaluationRef != LastEvaluationRef || target.OutcomeRef == Guid.Empty ||
            target.ContentVersion != ContentVersion || target.ApprovalCycleId != ApprovalCycleId || target.RuleVersion < 1 ||
            string.IsNullOrWhiteSpace(target.RuleId) || (target.DistributionLine.HasValue && !_distributions.Any(d => d.LineNo == target.DistributionLine)))
            throw new PayablesException(PayablesErrors.OverrideMismatch);
        if (HasCurrentOverride(target)) throw new PayablesException(PayablesErrors.AlreadyOverridden);
        _overrides.Add(new(target, role, user, reason, at)); ChangeStamp++;
    }

    public bool HasCurrentOverride(OverrideTarget target) => Status is InvoiceStatus.Submitted or InvoiceStatus.Approved && target.ContentVersion == ContentVersion &&
        target.ApprovalCycleId == ApprovalCycleId && _overrides.Any(o => o.Target == target);

    public void Reevaluate(Guid evaluationRef, int contentVersion, string fingerprint, IReadOnlyList<ApprovalRequirement> requirements)
    {
        RequireActive(); CheckEvaluation(evaluationRef, fingerprint);
        var route = CheckRoute(requirements);
        if (contentVersion != ContentVersion) throw new PayablesException(PayablesErrors.StaleContent);
        if (fingerprint != RuleFingerprint || !route.ToHashSet().SetEquals(_requirements))
        {
            ApprovalCycleId = Guid.NewGuid();
            Status = InvoiceStatus.Submitted;
        }
        LastEvaluationRef = evaluationRef; RuleFingerprint = fingerprint; _requirements = route;
        ChangeStamp++;
    }

    public InvoiceRelease Reject(ApproverRole role, DepartmentCode? department, UserId user, string reason, DateTimeOffset at)
    {
        RequireActive(); CheckApprover(role, department, user); RequireReason(reason);
        var release = CaptureRelease();
        _approvals.Add(new(role, department, user, ApprovalDecision.Rejected, LastEvaluationRef!.Value,
            ContentVersion, ApprovalCycleId!.Value, RuleFingerprint!, reason, at));
        Status = InvoiceStatus.Rejected; ChangeStamp++;
        return release;
    }

    public void ReturnToDraft()
    {
        Require(InvoiceStatus.Rejected);
        ClearCycle(); ChangeStamp++;
    }

    public InvoiceRelease Withdraw(UserId user, string reason, DateTimeOffset at, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        RequireActive(); CheckUser(user); RequireReason(reason);
        if (user != CreatedBy) throw new PayablesException(PayablesErrors.OnlyAuthorWithdraws);
        var release = CaptureRelease();
        _withdrawals.Add(new(user, ApprovalCycleId!.Value, reason, at));
        ClearCycle(); ChangeStamp++;
        return release;
    }

    public void Post(Guid evaluationRef, int contentVersion, Guid cycle, string fingerprint, DateTimeOffset at)
    {
        Require(InvoiceStatus.Approved);
        if (evaluationRef != LastEvaluationRef || contentVersion != ContentVersion || cycle != ApprovalCycleId || fingerprint != RuleFingerprint)
            throw new PayablesException(PayablesErrors.PostingNotApproved);
        Status = InvoiceStatus.Posted; PostedAt = at; ChangeStamp++;
    }

    public void SetPaymentHold(bool hold) { PaymentHold = hold; ChangeStamp++; }

    /// <summary>
    /// Records that the payment journal entry may be written. Does not check cash or contact a bank.
    /// </summary>
    public void RecordPayment(bool vendorActive, DateOnly businessDate, DateTimeOffset at)
    {
        if (PaidAt is not null) throw new PayablesException(PayablesErrors.AlreadyPaid);
        if (!ReadyForPaymentHandoff(vendorActive, businessDate))
            throw new PayablesException(PayablesErrors.PaymentNotReady, ("status", Status), ("dueDate", DueDate));
        PaidAt = at;
        ChangeStamp++;
    }

    public bool ReadyForPaymentHandoff(bool vendorActive, DateOnly businessDate) =>
        Status == InvoiceStatus.Posted && PaidAt is null && vendorActive && !PaymentHold && DueDate <= businessDate;

    private IEnumerable<InvoiceApproval> CurrentApprovals() => _approvals.Where(a => a.Decision == ApprovalDecision.Approved &&
        a.ApprovalCycleId == ApprovalCycleId && a.ContentVersion == ContentVersion && a.RuleFingerprint == RuleFingerprint);
    private InvoiceRelease CaptureRelease() => new(Id, ContentVersion, ReservationRefs, EncumbranceClaimRefs, PoBillingClaimRefs);
    private void ClearCycle()
    {
        Status = InvoiceStatus.Draft; ApprovalCycleId = null; LastEvaluationRef = null; RuleFingerprint = null;
        ReservationRefs = Array.Empty<Guid>(); EncumbranceClaimRefs = Array.Empty<Guid>(); PoBillingClaimRefs = Array.Empty<Guid>();
        _requirements = Array.Empty<ApprovalRequirement>();
    }
    private void CheckApprover(ApproverRole role, DepartmentCode? department, UserId user)
    {
        CheckUser(user);
        if (user == CreatedBy || !_requirements.Contains(new ApprovalRequirement(role, department)))
            throw new PayablesException(PayablesErrors.ApprovalNotAllowed);
    }
    private static IReadOnlyList<ApprovalRequirement> CheckRoute(IReadOnlyList<ApprovalRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        if (requirements.Count == 0 || requirements.Any(r => r is null || !Enum.IsDefined(r.Role) ||
            (r.Role == ApproverRole.DepartmentHead) != (r.Department is not null)))
            throw new PayablesException(PayablesErrors.RouteInvalid);
        return Array.AsReadOnly(requirements.Distinct().ToArray());
    }
    private static IReadOnlyList<Guid> CopyIds(IReadOnlyList<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Contains(Guid.Empty) || ids.Distinct().Count() != ids.Count) throw new PayablesException(PayablesErrors.HoldIdsInvalid);
        return Array.AsReadOnly(ids.ToArray());
    }
    private static void CheckHeader(string number, Guid vendor, Money total, string? po)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        if (vendor == Guid.Empty || total <= Money.Zero || po is not null && string.IsNullOrWhiteSpace(po))
            throw new PayablesException(PayablesErrors.HeaderInvalid);
    }
    private static void CheckDates(DateOnly invoiceDate, DateOnly dueDate)
    {
        if (dueDate < invoiceDate) throw new PayablesException(PayablesErrors.DueBeforeInvoice, ("dueDate", dueDate), ("invoiceDate", invoiceDate));
    }
    private static void CheckEvaluation(Guid id, string fingerprint)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(fingerprint)) throw new PayablesException(PayablesErrors.EvaluationRequired);
    }
    private static void CheckUser(UserId user)
    {
        if (user.Value == Guid.Empty) throw new PayablesException(PayablesErrors.ActorRequired);
    }
    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new PayablesException(PayablesErrors.ReasonRequired);
    }
    private void Require(InvoiceStatus status)
    {
        if (Status != status) throw new PayablesException(PayablesErrors.WrongStatus, ("expected", status), ("status", Status));
    }
    private void RequireActive()
    {
        if (Status is not (InvoiceStatus.Submitted or InvoiceStatus.Approved)) throw new PayablesException(PayablesErrors.NoActiveCycle, ("status", Status));
    }
}
