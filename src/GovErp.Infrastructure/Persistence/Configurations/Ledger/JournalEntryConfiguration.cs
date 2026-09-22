using GovErp.Domain.Ledger.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Ledger;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> b)
    {
        b.ToTable("JournalEntries", "ledger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        // Один журнал на инвойс: вторая попытка Post падает на индексе.
        b.Property(x => x.SourceRef).HasMaxLength(100);
        b.HasIndex(x => x.SourceRef).IsUnique();
        b.Property(x => x.PostedBy).HasConversion(Conversions.User);
        b.OwnsMany(x => x.Lines, o =>
        {
            o.ToTable("JournalLines", "ledger");
            o.WithOwner().HasForeignKey("JournalEntryId");
            o.Property<int>("Id").ValueGeneratedOnAdd();
            o.HasKey("Id");
            o.Property(l => l.Account).HasConversion(Conversions.Account).HasMaxLength(64);
            o.Property(l => l.Family).HasConversion<string>().HasMaxLength(10);
            o.Property(l => l.Debit).HasConversion(Conversions.Money).HasPrecision(18, 2);
            o.Property(l => l.Credit).HasConversion(Conversions.Money).HasPrecision(18, 2);
            o.Property(l => l.Description).HasMaxLength(200);
        });
        b.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
