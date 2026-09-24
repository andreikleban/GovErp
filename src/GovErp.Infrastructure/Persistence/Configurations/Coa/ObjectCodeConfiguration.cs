using GovErp.Domain.ChartOfAccounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Coa;

/// <summary>
/// EF Core mapping for an object code.
/// </summary>
public sealed class ObjectCodeConfiguration : IEntityTypeConfiguration<ObjectCodeDefinition>
{
    public void Configure(EntityTypeBuilder<ObjectCodeDefinition> b)
    {
        b.ToTable("ObjectCodes", "coa");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasConversion(Conversions.Object).HasMaxLength(5);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
    }
}
