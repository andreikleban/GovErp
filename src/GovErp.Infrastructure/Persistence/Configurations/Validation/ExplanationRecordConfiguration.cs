using GovErp.Application.Web.Explanation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Validation;

public sealed class ExplanationRecordConfiguration : IEntityTypeConfiguration<ExplanationRecord>
{
    public void Configure(EntityTypeBuilder<ExplanationRecord> b)
    {
        b.ToTable("Explanations", "validation");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => x.EvaluationId);
        b.Property(x => x.Audience).HasConversion<string>().HasMaxLength(30);
    }
}
