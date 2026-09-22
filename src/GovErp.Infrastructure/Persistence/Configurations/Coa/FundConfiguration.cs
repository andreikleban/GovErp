using GovErp.Domain.ChartOfAccounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Coa;

public sealed class FundConfiguration : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> b)
    {
        b.ToTable("Funds", "coa");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasConversion(Conversions.Fund).HasMaxLength(3);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Basis).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ControlMode).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.GrantPolicy).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.AllowedDepartments).HasConversion(CodeList.Converter<DepartmentCode>(s => new DepartmentCode(s)), CodeList.Comparer<DepartmentCode>()).HasMaxLength(500);
        b.Property(x => x.AllowedObjects).HasConversion(CodeList.Converter<ObjectCode>(s => new ObjectCode(s)), CodeList.Comparer<ObjectCode>()).HasMaxLength(500);
    }
}
