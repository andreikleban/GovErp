using GovErp.Domain.Ledger.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Ledger;

/// <summary>
/// EF Core mapping for a fiscal period.
/// </summary>
public sealed class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalPeriod> b)
    {
        b.ToTable("FiscalPeriods", "ledger");
        b.HasKey(x => new { x.Year, x.Month });
        b.Property(x => x.Year).ValueGeneratedNever();
        b.Property(x => x.Month).ValueGeneratedNever();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
        b.Ignore(x => x.IsOpen);
    }
}
