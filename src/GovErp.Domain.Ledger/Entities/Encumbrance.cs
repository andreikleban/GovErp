using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// The encumbrance of a purchase-order line: remaining, claims and liquidation.
/// </summary>
public sealed class Encumbrance
{
    private readonly List<EncumbranceLiquidation> _liquidations = [];
    private readonly List<EncumbranceClaim> _claims = [];
    private readonly List<PoBillingClaim> _billingClaims = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string PoLineRef { get; private set; }
    public AccountCode Account { get; private set; }
    public Money Original { get; private set; }
    public Money Liquidated { get; private set; }
    public Money Released { get; private set; }
    public Money AuthorizedPoAmount { get; private set; }
    public Money AlreadyPostedAgainstPo { get; private set; }
    public long ChangeStamp { get; private set; }
    public EncumbranceStatus Status { get; private set; } = EncumbranceStatus.Open;
    public Money Remaining => Original - Liquidated - Released;
    public IReadOnlyList<EncumbranceLiquidation> Liquidations => _liquidations.AsReadOnly();
    public IReadOnlyList<EncumbranceClaim> Claims => _claims.AsReadOnly();
    public IReadOnlyList<PoBillingClaim> BillingClaims => _billingClaims.AsReadOnly();
    private Money Held => _claims.Where(c => c.Status == ClaimStatus.Held).Aggregate(Money.Zero, (s, c) => s + c.Amount);

    private Encumbrance() { PoLineRef = null!; Account = null!; }
    public Encumbrance(string poLineRef, AccountCode account, Money original) : this(poLineRef, account, original, original, Money.Zero) { }
    public Encumbrance(string poLineRef, AccountCode account, Money original, Money authorizedPoAmount, Money alreadyPostedAgainstPo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poLineRef);
        ArgumentNullException.ThrowIfNull(account);
        LedgerGuard.Positive(original);
        LedgerGuard.Positive(authorizedPoAmount);
        if (alreadyPostedAgainstPo.IsNegative || original > authorizedPoAmount) throw new LedgerException(LedgerErrors.InvalidPoOpening);
        PoLineRef = poLineRef; Account = account; Original = original; AuthorizedPoAmount = authorizedPoAmount; AlreadyPostedAgainstPo = alreadyPostedAgainstPo;
    }
    public Money ClaimableForInvoice(Guid invoiceId, int contentVersion)
    {
        LedgerGuard.Owner(invoiceId, contentVersion);
        return Remaining - _claims.Where(c => c.Status == ClaimStatus.Held && (c.InvoiceId != invoiceId || c.ContentVersion != contentVersion)).Aggregate(Money.Zero, (s, c) => s + c.Amount);
    }
    public Guid Claim(Guid invoiceId, int contentVersion, Money amount)
    {
        LedgerGuard.Owner(invoiceId, contentVersion); LedgerGuard.Positive(amount);
        if (Status != EncumbranceStatus.Open || amount > Remaining - Held) throw new LedgerException(LedgerErrors.InsufficientEncumbrance, ("poLine", PoLineRef), ("amount", amount));
        var claim = new EncumbranceClaim(invoiceId, contentVersion, amount);
        _claims.Add(claim); ChangeStamp++; return claim.Id;
    }
    public Guid ClaimBilling(Guid invoiceId, int contentVersion, Money amount, decimal tolerance)
    {
        LedgerGuard.Owner(invoiceId, contentVersion); LedgerGuard.Positive(amount);
        if (tolerance is < 0 or > 1) throw new LedgerException(LedgerErrors.InvalidTolerance);
        var projected = AlreadyPostedAgainstPo + _billingClaims.Where(c => c.Status == ClaimStatus.Held).Aggregate(Money.Zero, (s, c) => s + c.Amount) + amount;
        if (projected.Amount > AuthorizedPoAmount.Amount * (1 + tolerance)) throw new LedgerException(LedgerErrors.PoBillingExceedsTolerance, ("poLine", PoLineRef));
        var claim = new PoBillingClaim(invoiceId, contentVersion, amount);
        _billingClaims.Add(claim); ChangeStamp++; return claim.Id;
    }
    public void ConsumeClaim(Guid id, Guid? invoiceId = null, int? contentVersion = null)
    {
        var claim = FindClaim(id, invoiceId, contentVersion); var liquidated = Liquidated + claim.Amount;
        claim.Complete(ClaimStatus.Consumed); Liquidated = liquidated;
        _liquidations.Add(new(claim.Amount, claim.InvoiceId.ToString()));
        if (Remaining.IsZero) Status = EncumbranceStatus.Closed;
        ChangeStamp++;
    }
    public void ReleaseClaim(Guid id, Guid? invoiceId = null, int? contentVersion = null)
    { FindClaim(id, invoiceId, contentVersion).Complete(ClaimStatus.Released); ChangeStamp++; }
    public void ConsumeBillingClaim(Guid id, Guid? invoiceId = null, int? contentVersion = null)
    {
        var claim = FindBilling(id, invoiceId, contentVersion); var posted = AlreadyPostedAgainstPo + claim.Amount;
        claim.Complete(ClaimStatus.Consumed); AlreadyPostedAgainstPo = posted; ChangeStamp++;
    }
    public void ReleaseBillingClaim(Guid id, Guid? invoiceId = null, int? contentVersion = null)
    { FindBilling(id, invoiceId, contentVersion).Complete(ClaimStatus.Released); ChangeStamp++; }
    private EncumbranceClaim FindClaim(Guid id, Guid? owner, int? version)
    {
        var c = _claims.SingleOrDefault(c => c.Id == id) ?? throw new LedgerException(LedgerErrors.UnknownClaim);
        CheckHeld(c.Status, c.InvoiceId, c.ContentVersion, owner, version); return c;
    }
    private PoBillingClaim FindBilling(Guid id, Guid? owner, int? version)
    {
        var c = _billingClaims.SingleOrDefault(c => c.Id == id) ?? throw new LedgerException(LedgerErrors.UnknownBillingClaim);
        CheckHeld(c.Status, c.InvoiceId, c.ContentVersion, owner, version); return c;
    }
    private static void CheckHeld(ClaimStatus status, Guid owner, int version, Guid? expectedOwner, int? expectedVersion)
    {
        if (status != ClaimStatus.Held || expectedOwner.HasValue != expectedVersion.HasValue || (expectedOwner.HasValue && (owner != expectedOwner || version != expectedVersion)))
            throw new LedgerException(LedgerErrors.ClaimNotHeld);
    }
    public void Liquidate(Money amount, string sourceRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef); LedgerGuard.Positive(amount);
        if (Status == EncumbranceStatus.Closed || amount > Remaining - Held) throw new LedgerException(LedgerErrors.LiquidationExceedsRemaining, ("poLine", PoLineRef), ("amount", amount));
        Liquidated += amount; _liquidations.Add(new(amount, sourceRef));
        if (Remaining.IsZero) Status = EncumbranceStatus.Closed;
        ChangeStamp++;
    }
    public void ReleaseRemainder(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status == EncumbranceStatus.Closed || !Held.IsZero) throw new LedgerException(LedgerErrors.EncumbranceNotReleasable, ("poLine", PoLineRef));
        Released += Remaining; Status = EncumbranceStatus.Closed; ChangeStamp++;
    }
}
