using GovErp.Application.Web.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Audit;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("Events", "audit");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.TenantId).HasConversion(Conversions.Tenant);
        b.Property(x => x.Actor).HasConversion(Conversions.User);
        b.Property(x => x.SubjectRef).HasMaxLength(100);
        b.HasIndex(x => x.SubjectRef);
        b.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
    }
}
