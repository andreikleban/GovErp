using System.Globalization;
using System.Text;
using GovErp.Application.Web.Explanation;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Infrastructure.Explanation;

/// <summary>
/// A deterministic explanation from the EvaluationRecord alone: no external calls and no database access.
/// Steps come from record.Steps (the pipeline marks skipped ones itself), transaction data from InputSnapshot.
/// </summary>
public sealed class TemplateExplanationGenerator : IExplanationGenerator
{
    public const string ProviderName = "Template";
    public const string TemplateVersion = "template-1";
    private const int ShortFingerprintLength = 12;

    public Task<ExplanationResult> ExplainAsync(EvaluationRecord record, ExplanationAudience audience, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Task.FromResult(new ExplanationResult(Build(record, audience), ProviderName, null, TemplateVersion, null));
    }

    private static string Build(EvaluationRecord record, ExplanationAudience audience)
    {
        var isPublic = audience == ExplanationAudience.Public;
        var isAuditor = audience == ExplanationAudience.Auditor;
        var subject = record.InputSnapshot;
        var tx = subject.Transaction;
        var text = new StringBuilder();

        text.Append(CultureInfo.InvariantCulture,
                $"Invoice {tx.TransactionRef} (content v{tx.ContentVersion}) for {tx.Total} from {tx.Vendor.Name} was evaluated on ")
            .Append(record.EvaluatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Append(CultureInfo.InvariantCulture, $" ({record.Trigger}). Result: {Label(record.Overall)}.\n");

        var funds = subject.Distributions.Select(FundLine).ToList();
        text.Append("Funds charged: ").Append(funds.Count == 0 ? "none" : string.Join("; ", funds)).Append(".\n");

        text.Append(ChecksLine(record)).Append('\n');

        var findings = record.Outcomes.Where(o => o.Severity != Severity.Allowed).ToList();
        if (findings.Count == 0)
        {
            text.Append("Findings: none.\n");
        }
        else
        {
            text.Append("Findings:\n");
            foreach (var outcome in findings)
            {
                AppendFinding(text, outcome, isAuditor);
            }
        }

        text.Append("Approvals required: ").Append(ApprovalsLine(record, subject, isPublic)).Append(".\n");
        text.Append("Accounting entries to be created: ").Append(EntriesLine(record)).Append(".\n");

        text.Append("Rule set: ").Append(record.RuleSetVersions.Engine);
        if (!isPublic)
        {
            var fingerprint = record.RuleFingerprint;
            text.Append(", fingerprint ").Append(isAuditor ? fingerprint : fingerprint[..Math.Min(ShortFingerprintLength, fingerprint.Length)]);
        }

        text.Append('.');
        return text.ToString();
    }

    private static string FundLine(DistributionSnapshot d)
    {
        var fund = d.Fund is { } f
            ? $"{f.Name}, {(f.Control == BudgetControl.Soft ? "soft" : "hard")} budget control"
            : "fund not resolved";
        var grant = d.Grant is { } g ? $", grant {g.Code}" : "";
        return string.Create(CultureInfo.InvariantCulture, $"line {d.LineNo} — {d.Amount} to {d.Account} ({fund}){grant}");
    }

    private static string ChecksLine(EvaluationRecord record)
    {
        var executed = record.Steps.Where(s => s.Status == StepExecutionStatus.Executed).Select(s => s.Step.ToString()).ToList();
        // PostingEligibility runs only on Post; other skips follow a Hard Stop.
        var skippedAfterStop = record.Steps
            .Where(s => s.Status == StepExecutionStatus.Skipped && s.Step != ValidationStep.PostingEligibility)
            .Select(s => s.Step.ToString()).ToList();
        var postingSkipped = record.Steps.Any(s => s.Step == ValidationStep.PostingEligibility && s.Status == StepExecutionStatus.Skipped);
        var line = new StringBuilder("Checks performed: steps ")
            .Append(executed.Count == 0 ? "none" : string.Join(", ", executed))
            .Append("; skipped after a hard stop: ")
            .Append(skippedAfterStop.Count == 0 ? "none" : string.Join(", ", skippedAfterStop));
        if (postingSkipped)
        {
            line.Append("; posting eligibility is checked only when posting");
        }

        return line.Append(CultureInfo.InvariantCulture, $"; {record.RuleSetVersions.AppliedRules.Count} rules applied.").ToString();
    }

    private static void AppendFinding(StringBuilder text, RuleOutcome outcome, bool isAuditor)
    {
        text.Append("- ").Append(outcome.RuleId).Append(" [").Append(Label(outcome.Severity)).Append(']');
        if (outcome.DistributionLine is { } line)
        {
            text.Append(CultureInfo.InvariantCulture, $" (line {line})");
        }

        text.Append(" — ").Append(Sentence(outcome.Message)).Append(" Required action: ").Append(Sentence(outcome.Resolution));
        if (outcome.OverriddenBy is { } overridden)
        {
            text.Append(" Overridden by ").Append(overridden.Role?.ToString() ?? "an authorized approver").Append('.');
        }

        text.Append('\n');
        if (isAuditor)
        {
            AppendValues(text, "inputs", outcome.Inputs);
            AppendValues(text, "computed", outcome.Computed);
        }
    }

    private static void AppendValues(StringBuilder text, string title, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        text.Append("  ").Append(title).Append(": ")
            .Append(string.Join("; ", values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key}={v.Value}")))
            .Append('\n');
    }

    private static string ApprovalsLine(EvaluationRecord record, ValidationSubject subject, bool isPublic)
    {
        if (record.ApprovalRoute.Count == 0)
        {
            return "none";
        }

        return string.Join("; ", record.ApprovalRoute.Select(r =>
        {
            var line = new StringBuilder(r.Role.ToString());
            if (!string.IsNullOrEmpty(r.Department))
            {
                line.Append(" (").Append(r.Department).Append(')');
            }

            line.Append(", ").Append(r.IsSatisfied ? "satisfied" : "pending");
            if (r.IsSatisfied && !isPublic)
            {
                var approvers = subject.DetailedApprovals
                    .Where(a => a.IsApproved && a.Role == r.Role && (r.Department is null || a.Department?.Value == r.Department))
                    .Select(a => a.UserId.ToString()).Distinct().ToList();
                if (approvers.Count > 0)
                {
                    line.Append(" by user ").Append(string.Join(", ", approvers));
                }
            }

            return line.ToString();
        }));
    }

    private static string EntriesLine(EvaluationRecord record)
    {
        if (record.PostingPreview.Count == 0)
        {
            return record.Overall == Severity.HardStop ? "none until the stop is resolved" : "none";
        }

        return string.Join(" / ", record.PostingPreview.Select(l => l.Debit.IsZero
            ? $"Cr {l.Account} {l.Credit}"
            : $"Dr {l.Account} {l.Debit}"));
    }

    private static string Label(Severity severity) => severity switch
    {
        Severity.HardStop => "HARD STOP",
        Severity.SoftStop => "SOFT STOP",
        Severity.Warning => "WARNING",
        _ => "ALLOWED",
    };

    private static string Sentence(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed.EndsWith('.') ? trimmed : trimmed + ".";
    }
}
