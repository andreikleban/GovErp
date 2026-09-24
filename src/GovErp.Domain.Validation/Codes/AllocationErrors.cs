namespace GovErp.Domain.Validation.Codes;

/// <summary>
/// Codes of budget and PO snapshots that cannot be allocated (reported by the budget and PO rules as Hard Stops).
/// </summary>
public static class AllocationErrors
{
    public const string DuplicateLine = "ALLOCATION.DUPLICATE_LINE";
    public const string ConflictingBudgetSnapshots = "ALLOCATION.CONFLICTING_BUDGET_SNAPSHOTS";
    public const string InconsistentBudget = "ALLOCATION.INCONSISTENT_BUDGET";
    public const string NonPoInvoiceWithEncumbrance = "ALLOCATION.NON_PO_INVOICE_WITH_ENCUMBRANCE";
    public const string ConflictingPoSnapshots = "ALLOCATION.CONFLICTING_PO_SNAPSHOTS";
    public const string PoLineClosed = "ALLOCATION.PO_LINE_CLOSED";
    public const string PoLineInvalid = "ALLOCATION.PO_LINE_INVALID";
    public const string InconsistentPoBalances = "ALLOCATION.INCONSISTENT_PO_BALANCES";
    public const string AmountNotPositive = "ALLOCATION.AMOUNT_NOT_POSITIVE";
    public const string PoLineSnapshotMissing = "ALLOCATION.PO_LINE_SNAPSHOT_MISSING";
    public const string FundFactsMissing = "ALLOCATION.FUND_FACTS_MISSING";
}
