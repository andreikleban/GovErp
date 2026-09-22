using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public static class PostingPreviewBuilder
{
    public static PostingPreview Build(ValidationSubject subject)
    {
        var allocation = BudgetAllocation.Allocate(subject);
        var failures = allocation.Where(a => a.Error is not null).Select(a => a.Error!).ToList();
        if (subject.Distributions.Any(d => d.Fund is null)) failures.Add("Fund facts are missing.");
        if (failures.Count > 0) return new([], failures);

        var lines = new List<PostingPreviewLine>();
        var accounts = subject.PostingAccounts;
        foreach (var item in allocation)
        {
            var distribution = item.Distribution;
            var account = distribution.Account;
            if (item.LiquidationAmount > Money.Zero)
            {
                lines.Add(new(new(account.Fund, accounts.BalanceSheetDepartment, accounts.ReserveForEncumbrances, account.Grant),
                    "Budgetary", item.LiquidationAmount, Money.Zero, "Reverse reserve for encumbrances"));
                lines.Add(new(account.WithObject(accounts.Encumbrances), "Budgetary", Money.Zero, item.LiquidationAmount, "Reverse encumbrances"));
            }
            lines.Add(new(account, "Financial", distribution.Amount, Money.Zero,
                distribution.Fund!.Kind == FundKind.Enterprise ? "Expense" : "Expenditure"));
        }
        foreach (var fund in subject.Distributions.GroupBy(d => d.Account.Fund))
        {
            var total = fund.Aggregate(Money.Zero, (sum, d) => sum + d.Amount);
            lines.Add(new(new(fund.Key, accounts.BalanceSheetDepartment, accounts.AccountsPayable, null),
                "Financial", Money.Zero, total, "Accounts payable"));
        }
        return new(lines);
    }

    public static bool IsBalancedPerFund(IReadOnlyList<PostingPreviewLine> lines) => lines.Count >= 2
        && lines.All(l => l.Family is "Financial" or "Budgetary" && !l.Debit.IsNegative && !l.Credit.IsNegative
            && (l.Debit > Money.Zero && l.Credit.IsZero || l.Credit > Money.Zero && l.Debit.IsZero))
        && lines.GroupBy(l => (l.Account.Fund, l.Family)).All(group =>
            group.Aggregate(Money.Zero, (sum, l) => sum + l.Debit) == group.Aggregate(Money.Zero, (sum, l) => sum + l.Credit));
}
