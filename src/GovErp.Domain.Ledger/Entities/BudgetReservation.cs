namespace GovErp.Domain.Ledger.Entities;

public sealed class BudgetReservation
{
    public Guid Id { get; }
    public Guid InvoiceId { get; }
    public int ContentVersion { get; }
    public string SourceRef { get; }
    public Money Amount { get; }
    public ReservationStatus Status { get; private set; }
    internal BudgetReservation(Guid id, Guid invoiceId, int contentVersion, string sourceRef, Money amount)
    { Id = id; InvoiceId = invoiceId; ContentVersion = contentVersion; SourceRef = sourceRef; Amount = amount; }
    internal void Commit() => Status = ReservationStatus.Committed;
    internal void Release() => Status = ReservationStatus.Released;
}
