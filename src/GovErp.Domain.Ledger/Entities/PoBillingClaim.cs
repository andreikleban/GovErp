namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// A claim on the cumulative billing of a purchase-order line.
/// </summary>
public sealed class PoBillingClaim
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InvoiceId { get; private set; }
    public int ContentVersion { get; private set; }
    public Money Amount { get; private set; }
    public ClaimStatus Status { get; private set; } = ClaimStatus.Held;
    private PoBillingClaim() { }
    internal PoBillingClaim(Guid invoiceId, int contentVersion, Money amount)
    { InvoiceId = invoiceId; ContentVersion = contentVersion; Amount = amount; }
    internal void Complete(ClaimStatus status) => Status = status;
}
