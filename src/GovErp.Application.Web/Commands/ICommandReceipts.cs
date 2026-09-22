namespace GovErp.Application.Web.Commands;

public interface ICommandReceipts
{
    Task<CommandReceipt?> FindAsync(Guid commandId, CancellationToken ct = default);
    void Add(CommandReceipt receipt);
}
