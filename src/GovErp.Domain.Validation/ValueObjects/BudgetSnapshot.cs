namespace GovErp.Domain.Validation.ValueObjects;

public sealed record BudgetSnapshot
{
    public BudgetSnapshot(bool Exists, Money Amended, Money Actuals, Money Encumbered,
        Money Held, Money Available, Money OwnHeld = default, int FiscalYear = 2026)
    {
        if (Available != Amended - Actuals - Encumbered - Held)
            throw new ArgumentException("Available must equal amended less actuals, encumbered and held.", nameof(Available));
        this.Exists = Exists;
        this.Amended = Amended;
        this.Actuals = Actuals;
        this.Encumbered = Encumbered;
        this.Held = Held;
        this.OwnHeld = OwnHeld;
        this.FiscalYear = FiscalYear;
    }

    public bool Exists { get; init; }
    public Money Amended { get; init; }
    public Money Actuals { get; init; }
    public Money Encumbered { get; init; }
    public Money Held { get; init; }
    public Money Available => Amended - Actuals - Encumbered - Held;
    public Money OwnHeld { get; init; }
    public int FiscalYear { get; init; }
    public static readonly BudgetSnapshot Missing = new(false, Money.Zero, Money.Zero, Money.Zero, Money.Zero, Money.Zero);
    public Money AvailableForThisInvoice => Available + OwnHeld;
}
