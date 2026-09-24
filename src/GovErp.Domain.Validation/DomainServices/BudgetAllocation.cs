using GovErp.Domain.Validation.Codes;
using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Allocates invoice lines onto budget keys and purchase-order lines.
/// </summary>
public static class BudgetAllocation
{
    /// <summary>
    /// One invoice line allocated: its PO liquidation, the new budget it needs, and an AllocationErrors code when its snapshots cannot be used.
    /// </summary>
    public sealed record Line(DistributionSnapshot Distribution, Money LiquidationAmount,
        Money RequiredNewBudget, string? Error);

    public static IReadOnlyList<Line> Allocate(ValidationSubject subject)
    {
        var ordered = subject.Distributions.OrderBy(x => x.LineNo).ToArray();
        var errors = new Dictionary<int, string>();
        foreach (var duplicate in ordered.GroupBy(x => x.LineNo).Where(x => x.Count() > 1))
            errors[duplicate.Key] = AllocationErrors.DuplicateLine;

        foreach (var group in ordered.GroupBy(x => (x.Account, x.Budget.FiscalYear)))
        {
            var first = group.First();
            if (group.Any(x => x.Budget != first.Budget || x.Fund?.Control != first.Fund?.Control))
                foreach (var d in group) errors[d.LineNo] = AllocationErrors.ConflictingBudgetSnapshots;
            else if (first.Budget.Amended < Money.Zero || first.Budget.Actuals < Money.Zero
                || first.Budget.Encumbered < Money.Zero || first.Budget.Held < Money.Zero
                || first.Budget.FiscalYear is < 2 or > 9999
                || first.Budget.OwnHeld < Money.Zero || first.Budget.OwnHeld > first.Budget.Held
                || first.Budget.Available != first.Budget.Amended - first.Budget.Actuals - first.Budget.Encumbered - first.Budget.Held)
                foreach (var d in group) errors[d.LineNo] = AllocationErrors.InconsistentBudget;
        }

        var remaining = new Dictionary<string, Money>(StringComparer.Ordinal);
        foreach (var group in ordered.Where(x => x.Encumbrance != null).GroupBy(x => x.Encumbrance!.PoLineRef))
        {
            var first = group.First();
            var po = first.Encumbrance!;
            string? error = null;
            if (!subject.Transaction.IsPoBacked) error = AllocationErrors.NonPoInvoiceWithEncumbrance;
            else if (group.Any(x => x.Encumbrance != po || x.Account != first.Account || x.Budget.FiscalYear != first.Budget.FiscalYear))
                error = AllocationErrors.ConflictingPoSnapshots;
            else if (!po.IsOpen) error = AllocationErrors.PoLineClosed;
            else if (string.IsNullOrWhiteSpace(po.PoLineRef) || po.AuthorizedPoAmount <= Money.Zero)
                error = AllocationErrors.PoLineInvalid;
            else if (po.Remaining < Money.Zero || po.Remaining > po.AuthorizedPoAmount
                || po.OwnLiquidationClaim.Amount > group.Sum(x => x.Amount.Amount)
                || po.OtherLiquidationClaims < Money.Zero
                || po.OwnLiquidationClaim < Money.Zero || po.AlreadyPostedAgainstPo < Money.Zero
                || po.OtherActiveInvoiceClaims < Money.Zero || po.OtherLiquidationClaims + po.OwnLiquidationClaim > po.Remaining)
                error = AllocationErrors.InconsistentPoBalances;
            if (error != null)
                foreach (var d in group) errors[d.LineNo] = error;
            remaining[group.Key] = error != null ? Money.Zero
                : subject.Transaction.Status is "Submitted" or "Approved" ? po.OwnLiquidationClaim : po.ClaimableForInvoice;
        }

        var result = new List<Line>();
        foreach (var d in ordered)
        {
            errors.TryGetValue(d.LineNo, out var error);
            if (d.Amount <= Money.Zero) error = AllocationErrors.AmountNotPositive;
            if (subject.Transaction.IsPoBacked && d.Encumbrance == null) error = AllocationErrors.PoLineSnapshotMissing;
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

    /// <summary>
    /// The invoice's demand on each budget line (account and fiscal year), in line order.
    /// </summary>
    public static IReadOnlyList<BudgetDemand> ByBudgetLine(ValidationSubject subject) =>
        Allocate(subject)
            .GroupBy(x => (x.Distribution.Account, x.Distribution.Budget.FiscalYear))
            .Select(g => new BudgetDemand(g.First().Distribution, Total(g, x => x.Distribution.Amount),
                Total(g, x => x.LiquidationAmount), Total(g, x => x.RequiredNewBudget), FirstError(g)))
            .ToList();

    /// <summary>
    /// The invoice's billing of each PO line it references, in line order.
    /// </summary>
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
