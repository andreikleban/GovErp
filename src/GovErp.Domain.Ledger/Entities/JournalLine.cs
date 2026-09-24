using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// A journal line: account, family, and either a debit or a credit.
/// </summary>
public sealed record JournalLine
{
    public AccountCode Account { get; }
    public LedgerFamily Family { get; }
    public Money Debit { get; }
    public Money Credit { get; }
    public string Description { get; }

    public JournalLine(AccountCode account, LedgerFamily family, Money debit, Money credit, string description)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (!Enum.IsDefined(family)) throw new LedgerException(LedgerErrors.UnknownFamily);
        if (debit.IsNegative || credit.IsNegative)
        {
            throw new LedgerException(LedgerErrors.NegativeJournalAmount);
        }

        if (debit.IsZero == credit.IsZero)
        {
            throw new LedgerException(LedgerErrors.DebitOrCredit);
        }

        Account = account;
        Family = family;
        Debit = debit;
        Credit = credit;
        Description = description;
    }
}
