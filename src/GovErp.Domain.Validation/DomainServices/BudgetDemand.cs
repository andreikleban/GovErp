using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>What the invoice asks from one budget line (account and fiscal year), summed over its invoice lines.</summary>
public sealed record BudgetDemand(DistributionSnapshot FirstLine, Money InvoiceAmount, Money Liquidation, Money RequiredNewBudget,
    string? Error)
{
    public AccountCode Account => FirstLine.Account;

    public BudgetSnapshot Budget => FirstLine.Budget;

    public BudgetControl? Control => FirstLine.Fund?.Control;

    /// <summary>Available after the invoice: available + the invoice's own reservation − the new budget it requires.</summary>
    public Money AvailableAfter => Budget.AvailableForThisInvoice - RequiredNewBudget;

    public Money Overage => Money.Max(Money.Zero, -AvailableAfter);

    /// <summary>AvailableAfter as a share of the amended budget; 1 when there is no amended budget to compare with.</summary>
    public decimal RemainingShare => Budget.Amended > Money.Zero ? AvailableAfter.Amount / Budget.Amended.Amount : 1m;
}
