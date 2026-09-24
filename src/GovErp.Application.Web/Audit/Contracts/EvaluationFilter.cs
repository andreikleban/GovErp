namespace GovErp.Application.Web.Audit.Contracts;

/// <summary>Evaluation list filters; null means no restriction. Document is a case-insensitive substring of TransactionRef (document number);
/// Trigger and Overall match exactly, case-insensitive; RuleFingerprint is a substring of the fingerprint (the table shows it shortened).</summary>
public sealed record EvaluationFilter(string? Document = null, string? Trigger = null, string? Overall = null, string? RuleFingerprint = null)
{
    public static readonly EvaluationFilter None = new();
}
