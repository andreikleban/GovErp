using GovErp.Domain.ChartOfAccounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Coa;

/// <summary>
/// EF Core mapping for a grant.
/// </summary>
public sealed class GrantConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> b)
    {
        b.ToTable("Grants", "coa");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasConversion(Conversions.Grant).HasMaxLength(32);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Sponsor).HasMaxLength(200);
        b.OwnsOne(x => x.Period, p =>
        {
            p.Property(x => x.From).HasColumnName("PeriodFrom");
            p.Property(x => x.To).HasColumnName("PeriodTo");
        });
        b.Navigation(x => x.Period).IsRequired();
        b.Property(x => x.AllowedDepartments).HasConversion(CodeList.Converter<DepartmentCode>(s => new DepartmentCode(s)), CodeList.Comparer<DepartmentCode>()).HasMaxLength(500);
        b.Property(x => x.AllowableObjects).HasConversion(CodeList.Converter<ObjectCode>(s => new ObjectCode(s)), CodeList.Comparer<ObjectCode>()).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    }
}
