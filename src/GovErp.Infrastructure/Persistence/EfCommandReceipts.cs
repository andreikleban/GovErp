using GovErp.Application.Web.Commands;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence;

/// <summary>
/// Stores command receipts in the ProcessedCommands table.
/// </summary>
public sealed class EfCommandReceipts(GovErpDbContext db) : ICommandReceipts
{
    public Task<CommandReceipt?> FindAsync(Guid commandId, CancellationToken ct = default) =>
        db.CommandReceipts.SingleOrDefaultAsync(r => r.CommandId == commandId, ct);

    public void Add(CommandReceipt receipt) => db.CommandReceipts.Add(receipt);
}
