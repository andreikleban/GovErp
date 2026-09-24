using GovErp.Domain.Ledger.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Ledger;

/// <summary>
/// EF Core mapping for an encumbrance.
/// </summary>
public sealed class EncumbranceConfiguration : IEntityTypeConfiguration<Encumbrance>
{
    public void Configure(EntityTypeBuilder<Encumbrance> b)
    {
        b.ToTable("Encumbrances", "ledger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.PoLineRef).HasMaxLength(60);
        b.HasIndex(x => x.PoLineRef).IsUnique();
        b.Property(x => x.Account).HasConversion(Conversions.Account).HasMaxLength(64);
        foreach (var money in new[] { nameof(Encumbrance.Original), nameof(Encumbrance.Liquidated), nameof(Encumbrance.Released),
                                      nameof(Encumbrance.AuthorizedPoAmount), nameof(Encumbrance.AlreadyPostedAgainstPo) })
        {
            b.Property<Money>(money).HasConversion(Conversions.Money).HasPrecision(18, 2);
        }

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.ChangeStamp);
        b.Ignore(x => x.Remaining);
        b.Property<byte[]>("RowVersion").IsRowVersion();

        b.OwnsMany(x => x.Liquidations, o =>
        {
            o.ToTable("EncumbranceLiquidations", "ledger");
            o.WithOwner().HasForeignKey("EncumbranceId");
            o.Property<int>("Id").ValueGeneratedOnAdd();
            o.HasKey("Id");
            o.Property(l => l.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
            o.Property(l => l.SourceRef).HasMaxLength(100);
        });
        b.Navigation(x => x.Liquidations).HasField("_liquidations").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Claims, o => Claim(o, "EncumbranceClaims"));
        b.Navigation(x => x.Claims).HasField("_claims").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.BillingClaims, o => Claim(o, "PoBillingClaims"));
        b.Navigation(x => x.BillingClaims).HasField("_billingClaims").UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    /// <summary>
    /// Liquidation claims and billing claims have the same shape: owning invoice id, content version, amount, status.
    /// </summary>
    private static void Claim<T>(OwnedNavigationBuilder<Encumbrance, T> o, string table) where T : class
    {
        o.ToTable(table, "ledger");
        o.WithOwner().HasForeignKey("EncumbranceId");
        o.Property<Guid>("Id").ValueGeneratedNever();
        o.HasKey("Id");
        o.HasIndex("InvoiceId");
        o.Property<int>("ContentVersion");
        o.Property<Money>("Amount").HasConversion(Conversions.Money).HasPrecision(18, 2);
        o.Property<ClaimStatus>("Status").HasConversion<string>().HasMaxLength(10);
    }
}
