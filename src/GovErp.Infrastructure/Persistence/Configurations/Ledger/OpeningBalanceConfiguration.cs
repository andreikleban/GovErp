using GovErp.Domain.Ledger.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Ledger;

public sealed class OpeningBalanceConfiguration : IEntityTypeConfiguration<OpeningBalance>
{
    public void Configure(EntityTypeBuilder<OpeningBalance> b)
    {
        b.ToTable("OpeningBalances", "ledger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Account).HasConversion(Conversions.Account).HasMaxLength(64);
        b.Property(x => x.FiscalYear).HasConversion(Conversions.FiscalYear);
        b.HasIndex(x => new { x.Account, x.FiscalYear }).IsUnique();
        b.Property(x => x.InitialActuals).HasConversion(Conversions.Money).HasPrecision(18, 2);
        b.Property(x => x.InitialEncumbered).HasConversion(Conversions.Money).HasPrecision(18, 2);
        b.Property(x => x.SourceReference).HasMaxLength(200);
    }
}
