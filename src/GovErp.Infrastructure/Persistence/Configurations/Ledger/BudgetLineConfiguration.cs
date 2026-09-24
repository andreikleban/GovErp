using GovErp.Domain.Ledger.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Ledger;

/// <summary>
/// EF Core mapping for a budget line.
/// </summary>
public sealed class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> b)
    {
        b.ToTable("BudgetLines", "ledger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Account).HasConversion(Conversions.Account).HasMaxLength(64);
        b.Property(x => x.FiscalYear).HasConversion(Conversions.FiscalYear);
        b.HasIndex(x => new { x.Account, x.FiscalYear }).IsUnique();
        b.Property(x => x.ControlMode).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Adopted).HasConversion(Conversions.Money).HasPrecision(18, 2);
        b.Property(x => x.Actuals).HasConversion(Conversions.Money).HasPrecision(18, 2);
        b.Property(x => x.Encumbered).HasConversion(Conversions.Money).HasPrecision(18, 2);
        b.Property(x => x.ChangeStamp);
        b.Property(x => x.OpeningBalanceId);
        b.Ignore(x => x.Amended).Ignore(x => x.Held).Ignore(x => x.Available);
        b.Property<byte[]>("RowVersion").IsRowVersion();
        b.OwnsMany(x => x.Amendments, o =>
        {
            o.ToTable("BudgetAmendments", "ledger");
            o.WithOwner().HasForeignKey("BudgetLineId");
            o.Property<int>("Id").ValueGeneratedOnAdd();
            o.HasKey("Id");
            o.Property(a => a.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
            o.Property(a => a.Reference).HasMaxLength(100);
        });
        b.Navigation(x => x.Amendments).HasField("_amendments").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Reservations, o =>
        {
            o.ToTable("BudgetReservations", "ledger");
            o.WithOwner().HasForeignKey("BudgetLineId");
            o.HasKey(r => r.Id);
            o.Property(r => r.Id).ValueGeneratedNever();
            o.HasIndex(r => r.InvoiceId);
            o.Property(r => r.SourceRef).HasMaxLength(100);
            o.Property(r => r.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
            o.Property(r => r.Status).HasConversion<string>().HasMaxLength(10);
        });
        b.Navigation(x => x.Reservations).HasField("_reservations").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
