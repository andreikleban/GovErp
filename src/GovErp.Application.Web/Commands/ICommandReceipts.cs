namespace GovErp.Application.Web.Commands;

/// <summary>
/// Looks up command receipts by CommandId so a repeated command is idempotent.
/// </summary>
public interface ICommandReceipts
{
    Task<CommandReceipt?> FindAsync(Guid commandId, CancellationToken ct = default);
    void Add(CommandReceipt receipt);
}
