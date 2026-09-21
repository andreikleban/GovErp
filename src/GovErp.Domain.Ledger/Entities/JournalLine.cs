using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

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
        if (!Enum.IsDefined(family)) throw new LedgerException("Unknown ledger family.");
        if (debit.IsNegative || credit.IsNegative)
        {
            throw new LedgerException("Debit and credit cannot be negative.");
        }

        if (debit.IsZero == credit.IsZero)
        {
            throw new LedgerException("A journal line must have exactly one of debit or credit.");
        }

        Account = account;
        Family = family;
        Debit = debit;
        Credit = credit;
        Description = description;
    }
}
