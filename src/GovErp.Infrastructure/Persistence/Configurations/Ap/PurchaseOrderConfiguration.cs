using GovErp.Domain.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Ap;

/// <summary>Без rowversion: PO в коде неизменяем, billing claims живут на Encumbrance (GE-17).</summary>
public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable("PurchaseOrders", "ap");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasMaxLength(50);
        b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Ignore(x => x.Total);
        b.OwnsMany(x => x.Lines, o =>
        {
            o.ToTable("PurchaseOrderLines", "ap");
            o.WithOwner().HasForeignKey("PurchaseOrderId");
            o.Property<int>("Id").ValueGeneratedOnAdd();
            o.HasKey("Id");
            o.Property(l => l.LineNo);
            o.Property(l => l.Account).HasConversion(Conversions.Account).HasMaxLength(64);
            o.Property(l => l.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
        });
        b.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
