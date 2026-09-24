namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// An invoice line with every fact the rules need.
/// </summary>
public sealed record DistributionSnapshot(int LineNo, AccountCode Account, Money Amount,
    CombinationSnapshot Combination, FundSnapshot? Fund, GrantSnapshot? Grant,
    BudgetSnapshot Budget, EncumbranceSnapshot? Encumbrance, Money AllocatedLiquidation = default)
{
    // Allocation across all lines sharing a PO is supplied by the pipeline.
    public Money LiquidationAmount => AllocatedLiquidation;
    public Money Excess => Amount - LiquidationAmount;
    public Money AmountToCheck => Excess;
    public Money RequiredNewBudget => AmountToCheck;
    public Money ProjectedAvailable => Budget.AvailableForThisInvoice - RequiredNewBudget;
}
