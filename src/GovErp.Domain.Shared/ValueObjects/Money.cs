using System.Globalization;

namespace GovErp.Domain.Shared.ValueObjects;

public readonly record struct Money : IComparable<Money>
{
    private const decimal Limit = 9999999999999999.99m;

    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < -Limit || amount > Limit)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must fit decimal(18,2).");
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Amount cannot contain fractional cents.", nameof(amount));
        Amount = amount;
    }

    public static readonly Money Zero = new(0m);
    public static Money Of(decimal amount) => new(amount);
    public bool IsNegative => Amount < 0;
    public bool IsZero => Amount == 0;
    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
    public static Money operator -(Money a) => new(-a.Amount);
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;
    public static Money Min(Money a, Money b) => a < b ? a : b;
    public static Money Max(Money a, Money b) => a > b ? a : b;
    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);
    public override string ToString() => Amount.ToString("N2", CultureInfo.InvariantCulture);
}
