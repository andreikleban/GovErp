using GovErp.Domain.ChartOfAccounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Coa;

public sealed class AccountCombinationConfiguration : IEntityTypeConfiguration<AccountCombination>
{
    public void Configure(EntityTypeBuilder<AccountCombination> b)
    {
        b.ToTable("AccountCombinations", "coa");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasConversion(Conversions.Account).HasMaxLength(64);
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.CreatedBy).HasConversion(Conversions.User);
        b.Property(x => x.ApprovedBy).HasConversion(Conversions.User);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
    }
}
