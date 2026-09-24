using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.RuleSupport;

/// <summary>A rule's decision about one item: the severity (null means as configured) and the reason shown to the user.</summary>
public sealed record Verdict(Severity? Severity, string Reason)
{
    /// <summary>On one invoice line; the line number, account and amount are recorded as its first inputs.</summary>
    public Finding On(DistributionSnapshot line) => new Finding(this, line.LineNo)
        .InputAs("line", line.LineNo)
        .InputAs("account", line.Account)
        .InputAs("amount", line.Amount);

    /// <summary>On a group of lines (a budget line, a PO line), reported on the group's first invoice line.</summary>
    public Finding OnGroup(DistributionSnapshot firstLine) => new(this, firstLine.LineNo);

    /// <summary>On the invoice as a whole.</summary>
    public Finding OnInvoice() => new(this, null);
}
