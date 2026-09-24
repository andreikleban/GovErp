using GovErp.Application.Web.Commands;

namespace GovErp.Infrastructure.Persistence;

/// <summary>
/// Sets OriginalValue of the shadow RowVersion: a stale form causes DbUpdateConcurrencyException, which the runner turns into Conflict.
/// Only BudgetLine, Encumbrance and VendorInvoice have a RowVersion; for other aggregates Expect does nothing and VersionOf is null.
/// </summary>
public sealed class EfConcurrencyGuard(GovErpDbContext db) : IConcurrencyGuard
{
    private const string RowVersion = "RowVersion";

    public void Expect(object aggregate, string? rowVersion)
    {
        if (rowVersion is not null && HasRowVersion(aggregate))
        {
            db.Entry(aggregate).Property(RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
        }
    }

    public string? VersionOf(object aggregate) =>
        HasRowVersion(aggregate) && db.Entry(aggregate).Property(RowVersion).CurrentValue is byte[] v ? Convert.ToBase64String(v) : null;

    private bool HasRowVersion(object aggregate) => db.Entry(aggregate).Metadata.FindProperty(RowVersion) is not null;
}
