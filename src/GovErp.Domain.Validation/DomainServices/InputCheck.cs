using GovErp.Domain.Validation.Codes;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// What the pipeline requires before any rule runs. A problem here is refused as VALIDATION_INPUT: an incomplete
/// snapshot must not produce a partial evaluation that looks like a decision.
/// </summary>
internal static class InputCheck
{
    private static readonly string[] KnownStatuses = ["Draft", "Submitted", "Approved", "Rejected", "Posted"];

    /// <summary>
    /// The first problem, or null when the input can be evaluated.
    /// </summary>
    public static Problem? FirstProblem(ValidationSubject subject, UserId evaluatedBy) =>
        (evaluatedBy.Value == Guid.Empty ? Of(InputErrors.ActorRequired) : null)
        ?? Lines(subject)
        ?? Document(subject.Transaction)
        ?? Dates(subject)
        ?? Facts(subject);

    private static Problem? Lines(ValidationSubject subject)
    {
        var lines = subject.Distributions;
        if (lines.Count == 0) return Of(InputErrors.NoLines);
        if (lines.Any(d => d.LineNo < 1)) return Of(InputErrors.LineNumberNotPositive);
        if (lines.GroupBy(d => d.LineNo).Any(g => g.Count() > 1)) return Of(InputErrors.LineNumberDuplicate);
        if (lines.Any(d => d.Amount <= Money.Zero)) return Of(InputErrors.LineAmountNotPositive);

        Money distributed;
        try
        {
            distributed = lines.Aggregate(Money.Zero, (sum, d) => sum + d.Amount);
        }
        catch (ArgumentException)
        {
            return Of(InputErrors.LineAmountsTooLarge);
        }

        return distributed != subject.Transaction.Total
            ? Of(InputErrors.LinesNotEqualTotal, ("distributed", distributed), ("total", subject.Transaction.Total))
            : null;
    }

    private static Problem? Document(TransactionSnapshot t)
    {
        if (t.ContentVersion < 1) return Of(InputErrors.ContentVersionNotPositive);
        if (t.CreatedBy.Value == Guid.Empty) return Of(InputErrors.AuthorRequired);
        if (!KnownStatuses.Contains(t.Status, StringComparer.Ordinal)) return Of(InputErrors.UnknownStatus, ("status", t.Status));
        if (t.Status is "Submitted" or "Approved" && t.ApprovalCycleId == Guid.Empty) return Of(InputErrors.ApprovalCycleRequired);
        return null;
    }

    private static Problem? Dates(ValidationSubject subject)
    {
        var t = subject.Transaction;
        if (t.ServiceDate is not { } service || t.PostingDate is not { } posting) return Of(InputErrors.DatesRequired);
        if (t.InvoiceDate != service || t.InvoiceDate != posting) return Of(InputErrors.DatesMustMatch);

        var fiscalYear = FiscalYear.FromDate(posting).Year;
        return subject.Distributions.Any(d => d.Budget.Exists && d.Budget.FiscalYear != fiscalYear)
            ? Of(InputErrors.BudgetYearMismatch, ("fiscalYear", fiscalYear))
            : null;
    }

    /// <summary>
    /// The snapshot carries every fact the rules decide on, so no rule has to guess about a missing fund, grant or vendor.
    /// </summary>
    private static Problem? Facts(ValidationSubject subject)
    {
        var t = subject.Transaction;
        if (t.Vendor is null || t.Vendor.VendorId == Guid.Empty) return Of(InputErrors.VendorNotIdentified);
        foreach (var line in subject.Distributions)
        {
            if (line.Combination is null) return Of(InputErrors.CombinationFactsMissing, ("line", line.LineNo));
            if (line.Fund is null) return Of(InputErrors.FundFactsMissing, ("line", line.LineNo), ("account", line.Account));
            if (line.Fund.Code != line.Account.Fund.Value)
                return Of(InputErrors.FundFactsMismatch, ("line", line.LineNo), ("fund", line.Fund.Code), ("accountFund", line.Account.Fund));
            if (line.Account.Grant is { } grant && line.Grant is null) return Of(InputErrors.GrantFactsMissing, ("line", line.LineNo), ("grant", grant));
            if (line.Account.Grant is { } coded && line.Grant is { } facts && facts.Code != coded.Value)
                return Of(InputErrors.GrantFactsMismatch, ("line", line.LineNo), ("grant", facts.Code), ("accountGrant", coded));
            if (t.IsPoBacked && line.Encumbrance is null) return Of(InputErrors.PoLineMissing, ("line", line.LineNo));
        }

        return null;
    }

    private static Problem Of(string code, params (string Name, object? Value)[] args) => Problem.Of(code, args);
}
