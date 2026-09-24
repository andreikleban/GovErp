namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// A hold on available budget for an invoice between submit and post.
/// </summary>
public sealed class BudgetReservation
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public int ContentVersion { get; private set; }
    public string SourceRef { get; private set; }
    public Money Amount { get; private set; }
    public ReservationStatus Status { get; private set; }
    private BudgetReservation() { SourceRef = null!; }
    internal BudgetReservation(Guid id, Guid invoiceId, int contentVersion, string sourceRef, Money amount)
    { Id = id; InvoiceId = invoiceId; ContentVersion = contentVersion; SourceRef = sourceRef; Amount = amount; }
    internal void Commit() => Status = ReservationStatus.Committed;
    internal void Release() => Status = ReservationStatus.Released;
}
