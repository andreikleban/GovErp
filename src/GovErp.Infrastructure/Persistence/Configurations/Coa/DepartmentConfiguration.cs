using GovErp.Domain.ChartOfAccounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Coa;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments", "coa");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasConversion(Conversions.Department).HasMaxLength(4);
        b.Property(x => x.Name).HasMaxLength(200);
    }
}
