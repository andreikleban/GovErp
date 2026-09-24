using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Payables.Entities;
using GovErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GovErp.Application.Web.Tests;

/// <summary>The model builds without a database connection: catches mapping errors before migrations and integration tests.</summary>
public class EfModelTests
{
    private static IModel Model()
    {
        using var db = new GovErpDbContext(new DbContextOptionsBuilder<GovErpDbContext>().UseSqlServer("Server=.;Database=x").Options);
        return db.Model;
    }

    [Fact]
    public void Domain_assigned_keys_of_owned_reservations_and_claims_are_never_generated()
    {
        var model = Model();
        var line = model.FindEntityType(typeof(BudgetLine))!;
        var reservation = line.FindNavigation(nameof(BudgetLine.Reservations))!.TargetEntityType;
        reservation.FindPrimaryKey()!.Properties.Single().ValueGenerated.Should().Be(ValueGenerated.Never);
        var encumbrance = model.FindEntityType(typeof(Encumbrance))!;
        foreach (var nav in new[] { nameof(Encumbrance.Claims), nameof(Encumbrance.BillingClaims) })
        {
            encumbrance.FindNavigation(nav)!.TargetEntityType.FindPrimaryKey()!.Properties.Single().ValueGenerated.Should().Be(ValueGenerated.Never);
        }
    }

    [Fact]
    public void Row_version_guards_exactly_the_mutable_aggregates()
    {
        Model().GetEntityTypes().Where(t => t.FindProperty("RowVersion")?.IsConcurrencyToken == true)
            .Select(t => t.ClrType).Should().BeEquivalentTo([typeof(BudgetLine), typeof(Encumbrance), typeof(VendorInvoice)]);
    }
}
