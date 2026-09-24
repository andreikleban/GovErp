namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// The full document snapshot the validation rules run against.
/// </summary>
public sealed record ValidationSubject
{
    private readonly IReadOnlyList<DistributionSnapshot> _distributions = Array.Empty<DistributionSnapshot>();
    private readonly IReadOnlyList<ApproverRole> _approvals = Array.Empty<ApproverRole>();
    private readonly IReadOnlyList<OverrideSnapshot> _overrides = Array.Empty<OverrideSnapshot>();
    private readonly IReadOnlyList<ApprovalSnapshot> _detailedApprovals = Array.Empty<ApprovalSnapshot>();
    private readonly IReadOnlyList<RuleOutcome> _previousOutcomes = Array.Empty<RuleOutcome>();
    private readonly IReadOnlyList<PreviousEvaluation> _previousEvaluations = Array.Empty<PreviousEvaluation>();

    public ValidationSubject(TransactionSnapshot Transaction, IReadOnlyList<DistributionSnapshot> Distributions,
        IReadOnlyList<ApproverRole> ApprovalsSoFar, IReadOnlyList<OverrideSnapshot> OverridesSoFar,
        bool PeriodIsOpen, RuleSetVersions? VersionsAtLastApproval, PostingAccounts PostingAccounts,
        IReadOnlyList<ApprovalSnapshot>? DetailedApprovals = null, DateOnly? BusinessDate = null,
        IReadOnlyList<RuleOutcome>? PreviousOutcomes = null, Guid? PreviousEvaluationId = null)
    {
        this.Transaction = Transaction;
        this.Distributions = Distributions;
        this.ApprovalsSoFar = ApprovalsSoFar;
        this.OverridesSoFar = OverridesSoFar;
        this.PeriodIsOpen = PeriodIsOpen;
        this.VersionsAtLastApproval = VersionsAtLastApproval;
        this.PostingAccounts = PostingAccounts;
        this.DetailedApprovals = DetailedApprovals ?? [];
        this.BusinessDate = BusinessDate;
        this.PreviousOutcomes = PreviousOutcomes ?? [];
        this.PreviousEvaluationId = PreviousEvaluationId;
    }

    public TransactionSnapshot Transaction { get; init; }
    public IReadOnlyList<DistributionSnapshot> Distributions
    {
        get => _distributions;
        init => _distributions = Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<ApproverRole> ApprovalsSoFar
    {
        get => _approvals;
        init => _approvals = Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<OverrideSnapshot> OverridesSoFar
    {
        get => _overrides;
        init => _overrides = Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<ApprovalSnapshot> DetailedApprovals
    {
        get => _detailedApprovals;
        init => _detailedApprovals = Array.AsReadOnly(value.ToArray());
    }
    public bool PeriodIsOpen { get; init; }
    public IReadOnlyList<RuleOutcome> PreviousOutcomes
    {
        get => _previousOutcomes;
        init => _previousOutcomes = Array.AsReadOnly(value.ToArray());
    }
    public Guid? PreviousEvaluationId { get; init; }

    /// <summary>
    /// Evaluations referenced by active overrides when there is more than one (Soft Stops released at different times).
    /// The PreviousEvaluationId / PreviousOutcomes pair is the special case of a single evaluation.
    /// </summary>
    public IReadOnlyList<PreviousEvaluation> PreviousEvaluations
    {
        get => _previousEvaluations;
        init => _previousEvaluations = Array.AsReadOnly(value.ToArray());
    }
    public RuleSetVersions? VersionsAtLastApproval { get; init; }
    public PostingAccounts PostingAccounts { get; init; }
    public DateOnly? BusinessDate { get; init; }
}
