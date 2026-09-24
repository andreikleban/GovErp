using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 4. A non-PO invoice at or above the threshold needs a purchase order or a documented procurement exception.
/// </summary>
public sealed class ProcurementThresholdRule : ValidationRule
{
    private const string PurchaseOrderRequired = "PROCUREMENT_THRESHOLD.PURCHASE_ORDER_REQUIRED";

    private static readonly ParameterSpec Threshold = ParameterSpec.Amount("threshold", Stricter.WhenLower);

    public override string RuleId => "PROCUREMENT_THRESHOLD";

    public override IReadOnlyList<ParameterSpec> Parameters => [Threshold];

    protected override Severity DefaultSeverity => Severity.SoftStop;

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: the invoice as a whole.
        var invoice = subject.Transaction;
        var threshold = Money.Of(parameters.Read(Threshold));

        // Decide: a large purchase without a purchase order.
        Verdict verdict;
        if (!invoice.IsPoBacked && invoice.Total >= threshold)
            verdict = Fail(PurchaseOrderRequired);
        else
            yield break;

        // Evidence
        yield return verdict.OnInvoice()
            .Input(invoice.Total)
            .Input(threshold)
            .InputAs("poBacked", "false");
    }
}
