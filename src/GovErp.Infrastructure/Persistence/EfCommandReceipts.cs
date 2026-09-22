using GovErp.Application.Web.Commands;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence;

public sealed class EfCommandReceipts(GovErpDbContext db) : ICommandReceipts
{
    public Task<CommandReceipt?> FindAsync(Guid commandId, CancellationToken ct = default) =>
        db.CommandReceipts.SingleOrDefaultAsync(r => r.CommandId == commandId, ct);

    public void Add(CommandReceipt receipt) => db.CommandReceipts.Add(receipt);
}
