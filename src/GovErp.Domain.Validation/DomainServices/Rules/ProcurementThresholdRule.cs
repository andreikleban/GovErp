using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class ProcurementThresholdRule : IValidationRule
{
    public string RuleId => "PROCUREMENT_THRESHOLD";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var threshold = Money.Of(definition.DecimalParameter("threshold"));
        if (threshold <= Money.Zero)
        {
            throw new ValidationException($"Rule {RuleId}: threshold must be positive.");
        }
        var t = subject.Transaction;
        if (t.IsPoBacked || t.Total < threshold)
        {
            return [];
        }

        return [RuleOutcome.From(definition, definition.Severity ?? Severity.SoftStop, null,
            RuleSupport.Map(("total", t.Total.ToString()), ("threshold", threshold.ToString()), ("poBacked", "false")),
            RuleSupport.Map(),
            $"Non-PO invoice of {t.Total} meets the {threshold} procurement threshold; a purchase order or documented procurement exception is required.")];
    }
}

