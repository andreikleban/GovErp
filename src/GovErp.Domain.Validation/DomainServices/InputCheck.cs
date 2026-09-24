using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// What the pipeline requires before any rule runs. A problem here is refused as VALIDATION_INPUT: an incomplete
/// snapshot must not produce a partial evaluation that looks like a decision.
/// </summary>
internal static class InputCheck
{
    private static readonly string[] KnownStatuses = ["Draft", "Submitted", "Approved", "Rejected", "Posted"];

    /// <summary>The first problem, or null when the input can be evaluated.</summary>
    public static string? Problem(ValidationSubject subject, UserId evaluatedBy) =>
        (evaluatedBy.Value == Guid.Empty ? "The evaluating actor is required." : null)
        ?? Lines(subject)
        ?? Document(subject.Transaction)
        ?? Dates(subject)
        ?? Facts(subject);

    private static string? Lines(ValidationSubject subject)
    {
        var lines = subject.Distributions;
        if (lines.Count == 0) return "The invoice has no distributions.";
        if (lines.Any(d => d.LineNo < 1)) return "Distribution line numbers must be positive.";
        if (lines.GroupBy(d => d.LineNo).Any(g => g.Count() > 1)) return "Distribution line numbers must be unique.";
        if (lines.Any(d => d.Amount <= Money.Zero)) return "Distribution amounts must be positive.";

        Money distributed;
        try
        {
            distributed = lines.Aggregate(Money.Zero, (sum, d) => sum + d.Amount);
        }
        catch (ArgumentException)
        {
            return "Distribution amounts exceed decimal(18,2).";
        }

        return distributed != subject.Transaction.Total
            ? $"Distributions {distributed} do not equal the invoice total {subject.Transaction.Total}."
            : null;
    }

    private static string? Document(TransactionSnapshot t)
    {
        if (t.ContentVersion < 1) return "Content version must be positive.";
        if (t.CreatedBy.Value == Guid.Empty) return "The invoice author is required.";
        if (!KnownStatuses.Contains(t.Status, StringComparer.Ordinal)) return $"Unknown document status '{t.Status}'.";
        if (t.Status is "Submitted" or "Approved" && t.ApprovalCycleId == Guid.Empty) return "An active document requires an approval cycle.";
        return null;
    }

    private static string? Dates(ValidationSubject subject)
    {
        var t = subject.Transaction;
        if (t.ServiceDate is not { } service || t.PostingDate is not { } posting) return "Service and posting dates are required.";
        if (t.InvoiceDate != service || t.InvoiceDate != posting) return "This demo supports only InvoiceDate = ServiceDate = PostingDate.";

        var fiscalYear = FiscalYear.FromDate(posting).Year;
        return subject.Distributions.Any(d => d.Budget.Exists && d.Budget.FiscalYear != fiscalYear)
            ? $"Budget snapshots must belong to FY{fiscalYear} of the posting date."
            : null;
    }

    /// <summary>The snapshot carries every fact the rules decide on, so no rule has to guess about a missing fund, grant or vendor.</summary>
    private static string? Facts(ValidationSubject subject)
    {
        var t = subject.Transaction;
        if (t.Vendor is null || t.Vendor.VendorId == Guid.Empty) return "The invoice vendor is not identified.";
        foreach (var line in subject.Distributions)
        {
            if (line.Combination is null) return $"Line {line.LineNo}: account combination facts are missing.";
            if (line.Fund is null) return $"Line {line.LineNo}: fund facts are missing for {line.Account}.";
            if (line.Fund.Code != line.Account.Fund.Value)
                return $"Line {line.LineNo}: fund facts {line.Fund.Code} do not match the account fund {line.Account.Fund}.";
            if (line.Account.Grant is { } grant && line.Grant is null) return $"Line {line.LineNo}: grant facts are missing for {grant}.";
            if (line.Account.Grant is { } coded && line.Grant is { } facts && facts.Code != coded.Value)
                return $"Line {line.LineNo}: grant facts {facts.Code} do not match the account grant {coded}.";
            if (t.IsPoBacked && line.Encumbrance is null) return $"Line {line.LineNo} of a PO-backed invoice is not linked to a PO line.";
        }

        return null;
    }
}
