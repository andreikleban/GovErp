using GovErp.Domain.Validation.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Validation;

public sealed class RuleDefinitionConfiguration : IEntityTypeConfiguration<RuleDefinition>
{
    public void Configure(EntityTypeBuilder<RuleDefinition> b)
    {
        b.ToTable("RuleDefinitions", "validation");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.RuleId).HasMaxLength(64);
        b.Property(x => x.Step).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Layer).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ScopeFund).HasMaxLength(3);
        b.Property(x => x.ScopeGrant).HasMaxLength(32);
        b.Property(x => x.Parameters).AsJson();
        b.Property(x => x.OverridableBy).AsJson();
        // Без фильтра: по умолчанию EF на SQL Server фильтрует IS NOT NULL, и правила без scope выпали бы из проверки.
        b.HasIndex(x => new { x.RuleId, x.Layer, x.Version, x.ScopeFund, x.ScopeGrant }).IsUnique().HasFilter(null);
    }
}
