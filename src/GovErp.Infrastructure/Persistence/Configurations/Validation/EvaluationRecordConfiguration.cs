using GovErp.Domain.Validation.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Validation;

public sealed class EvaluationRecordConfiguration : IEntityTypeConfiguration<EvaluationRecord>
{
    public void Configure(EntityTypeBuilder<EvaluationRecord> b)
    {
        b.ToTable("EvaluationRecords", "validation");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.TransactionRef).HasMaxLength(100);
        b.HasIndex(x => x.TransactionRef);
        b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Overall).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.EvaluatedBy).HasConversion(Conversions.User);
        b.Property(x => x.RuleFingerprint).HasMaxLength(64);
        b.Property(x => x.RuleSetVersions).AsJson();
        b.Property(x => x.Outcomes).AsJson();
        b.Property(x => x.Capabilities).AsJson();
        b.Property(x => x.Steps).AsJson();
        b.Property(x => x.ApprovalRoute).AsJson();
        b.Property(x => x.PostingPreview).AsJson();
        b.Property(x => x.PostingCheck).AsJson();
        b.Property(x => x.InputSnapshot).AsJson();
    }
}
