using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public static class BudgetAllocation
{
    public sealed record Line(DistributionSnapshot Distribution, Money LiquidationAmount,
        Money RequiredNewBudget, string? Error);

    public static IReadOnlyList<Line> Allocate(ValidationSubject subject)
    {
        var ordered = subject.Distributions.OrderBy(x => x.LineNo).ToArray();
        var errors = new Dictionary<int, string>();
        foreach (var duplicate in ordered.GroupBy(x => x.LineNo).Where(x => x.Count() > 1))
            errors[duplicate.Key] = "Duplicate distribution line number.";

        foreach (var group in ordered.GroupBy(x => (x.Account, x.Budget.FiscalYear)))
        {
            var first = group.First();
            if (group.Any(x => x.Budget != first.Budget || x.Fund?.Control != first.Fund?.Control))
                foreach (var d in group) errors[d.LineNo] = "Conflicting snapshots for the same budget account and fiscal year.";
            else if (first.Budget.Amended < Money.Zero || first.Budget.Actuals < Money.Zero
                || first.Budget.Encumbered < Money.Zero || first.Budget.Held < Money.Zero
                || first.Budget.FiscalYear is < 2 or > 9999
                || first.Budget.OwnHeld < Money.Zero || first.Budget.OwnHeld > first.Budget.Held
                || first.Budget.Available != first.Budget.Amended - first.Budget.Actuals - first.Budget.Encumbered - first.Budget.Held)
                foreach (var d in group) errors[d.LineNo] = "Inconsistent budget balances or own held reservation.";
        }

        var remaining = new Dictionary<string, Money>(StringComparer.Ordinal);
        foreach (var group in ordered.Where(x => x.Encumbrance != null).GroupBy(x => x.Encumbrance!.PoLineRef))
        {
            var first = group.First();
            var po = first.Encumbrance!;
            string? error = null;
            if (!subject.Transaction.IsPoBacked) error = "Non-PO invoice cannot reference an encumbrance.";
            else if (group.Any(x => x.Encumbrance != po || x.Account != first.Account || x.Budget.FiscalYear != first.Budget.FiscalYear))
                error = "Conflicting PO snapshots or PO budget account/fiscal year mismatch.";
            else if (!po.IsOpen) error = "PO line is closed.";
            else if (string.IsNullOrWhiteSpace(po.PoLineRef) || po.AuthorizedPoAmount <= Money.Zero)
                error = "PO line requires a positive authorized amount and reference.";
            else if (po.Remaining < Money.Zero || po.Remaining > po.AuthorizedPoAmount
                || po.OwnLiquidationClaim.Amount > group.Sum(x => x.Amount.Amount)
                || po.OtherLiquidationClaims < Money.Zero
                || po.OwnLiquidationClaim < Money.Zero || po.AlreadyPostedAgainstPo < Money.Zero
                || po.OtherActiveInvoiceClaims < Money.Zero || po.OtherLiquidationClaims + po.OwnLiquidationClaim > po.Remaining)
                error = "Inconsistent PO balances or liquidation claims.";
            if (error != null)
                foreach (var d in group) errors[d.LineNo] = error;
            remaining[group.Key] = error != null ? Money.Zero
                : subject.Transaction.Status is "Submitted" or "Approved" ? po.OwnLiquidationClaim : po.ClaimableForInvoice;
        }

        var result = new List<Line>();
        foreach (var d in ordered)
        {
            errors.TryGetValue(d.LineNo, out var error);
            if (d.Amount <= Money.Zero) error = "Invoice distribution amount must be positive.";
            if (subject.Transaction.IsPoBacked && d.Encumbrance == null) error = "Missing PO line snapshot.";
            var liquidation = Money.Zero;
            if (error == null && d.Encumbrance is { } po)
            {
                liquidation = Money.Min(d.Amount, remaining[po.PoLineRef]);
                remaining[po.PoLineRef] -= liquidation;
            }
            result.Add(new(d, liquidation, d.Amount - liquidation, error));
        }
        return result.AsReadOnly();
    }

    /// <summary>The invoice's demand on each budget line (account and fiscal year), in line order.</summary>
    public static IReadOnlyList<BudgetDemand> ByBudgetLine(ValidationSubject subject) =>
        Allocate(subject)
            .GroupBy(x => (x.Distribution.Account, x.Distribution.Budget.FiscalYear))
            .Select(g => new BudgetDemand(g.First().Distribution, Total(g, x => x.Distribution.Amount),
                Total(g, x => x.LiquidationAmount), Total(g, x => x.RequiredNewBudget), FirstError(g)))
            .ToList();

    /// <summary>The invoice's billing of each PO line it references, in line order.</summary>
    public static IReadOnlyList<PoLineBilling> ByPoLine(ValidationSubject subject) =>
        Allocate(subject)
            .Where(x => x.Distribution.Encumbrance is not null)
            .GroupBy(x => x.Distribution.Encumbrance!.PoLineRef)
            .Select(g => new PoLineBilling(g.First().Distribution, g.First().Distribution.Encumbrance!,
                Total(g, x => x.Distribution.Amount), Total(g, x => x.LiquidationAmount), FirstError(g)))
            .ToList();

    private static Money Total(IEnumerable<Line> lines, Func<Line, Money> amount) => new(lines.Sum(x => amount(x).Amount));

    private static string? FirstError(IEnumerable<Line> lines) => lines.Select(x => x.Error).FirstOrDefault(e => e is not null);
}
