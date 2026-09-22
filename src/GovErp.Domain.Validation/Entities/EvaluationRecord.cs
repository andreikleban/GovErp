using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Entities;

/// <summary>Неизменяемая запись одной оценки (GE-12). Создаётся только конвейером.</summary>
public sealed class EvaluationRecord
{
    public Guid Id { get; private set; }
    public string TransactionRef { get; private set; }
    public int TransactionVersion { get; private set; }
    public Guid ApprovalCycleId { get; private set; }
    public EvaluationTrigger Trigger { get; private set; }
    public DateTimeOffset EvaluatedAt { get; private set; }
    public UserId EvaluatedBy { get; private set; }
    public RuleSetVersions RuleSetVersions { get; private set; }
    public string RuleFingerprint { get; private set; }
    public IReadOnlyList<RuleOutcome> Outcomes { get; private set; }
    public Severity Overall { get; private set; }
    public Capabilities Capabilities { get; private set; }
    public IReadOnlyList<StepExecution> Steps { get; private set; }
    public IReadOnlyList<ApprovalRequirement> ApprovalRoute { get; private set; }
    public IReadOnlyList<PostingPreviewLine> PostingPreview { get; private set; }
    public PostingCheck? PostingCheck { get; private set; }
    public bool ReadyForPaymentHandoff { get; private set; }
    public ValidationSubject InputSnapshot { get; private set; }

    internal EvaluationRecord(ValidationSubject subject, EvaluationTrigger trigger, DateTimeOffset evaluatedAt, UserId evaluatedBy,
        RuleSetVersions ruleSetVersions, IReadOnlyList<RuleOutcome> outcomes, Severity overall, Capabilities capabilities,
        IReadOnlyList<StepExecution> steps, IReadOnlyList<ApprovalRequirement> approvalRoute,
        IReadOnlyList<PostingPreviewLine> postingPreview, PostingCheck? postingCheck, bool readyForPaymentHandoff)
    {
        Id = Guid.NewGuid();
        TransactionRef = subject.Transaction.TransactionRef;
        TransactionVersion = subject.Transaction.ContentVersion;
        ApprovalCycleId = subject.Transaction.ApprovalCycleId;
        Trigger = trigger;
        EvaluatedAt = evaluatedAt;
        EvaluatedBy = evaluatedBy;
        RuleSetVersions = ruleSetVersions;
        RuleFingerprint = ruleSetVersions.Fingerprint;
        Outcomes = Array.AsReadOnly(outcomes.ToArray());
        Overall = overall;
        Capabilities = capabilities;
        Steps = Array.AsReadOnly(steps.ToArray());
        ApprovalRoute = Array.AsReadOnly(approvalRoute.ToArray());
        PostingPreview = Array.AsReadOnly(postingPreview.ToArray());
        PostingCheck = postingCheck;
        ReadyForPaymentHandoff = readyForPaymentHandoff;
        InputSnapshot = subject;
    }

    private EvaluationRecord()
    {
        TransactionRef = null!;
        RuleSetVersions = null!;
        RuleFingerprint = null!;
        Outcomes = [];
        Capabilities = null!;
        Steps = [];
        ApprovalRoute = [];
        PostingPreview = [];
        InputSnapshot = null!;
    }
}
