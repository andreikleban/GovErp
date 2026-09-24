using GovErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Seed;

/// <summary>What to seed when provisioning a tenant: the full demo dataset or reference data only (no money movement).</summary>
public enum TenantSeed
{
    Springfield,
    ReferenceOnly
}

public static class TenantSeeder
{
    /// <summary>Seeds only an empty tenant database. Balances are OpeningBalance, not journal entries (spec §5, rule 7).</summary>
    public static async Task SeedAsync(GovErpDbContext db, TenantSeed seed, CancellationToken ct)
    {
        if (await db.Funds.AnyAsync(ct))
        {
            return;
        }

        var d = SpringfieldData.Create();
        db.Funds.AddRange(d.Funds);
        db.Departments.AddRange(d.Departments);
        db.ObjectCodes.AddRange(d.Objects);
        db.FiscalPeriods.AddRange(d.Periods);
        db.RuleDefinitions.AddRange(d.Rules);
        if (seed == TenantSeed.Springfield)
        {
            db.Grants.AddRange(d.Grants);
            db.AccountCombinations.AddRange(d.Combinations);
            db.OpeningBalances.AddRange(d.OpeningBalances);
            db.BudgetLines.AddRange(d.BudgetLines);
            db.Encumbrances.AddRange(d.Encumbrances);
            db.PurchaseOrders.AddRange(d.PurchaseOrders);
            db.Vendors.AddRange(d.Vendors);
        }

        await db.SaveChangesAsync(ct);
    }
}
