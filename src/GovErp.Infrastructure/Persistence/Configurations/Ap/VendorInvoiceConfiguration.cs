using GovErp.Domain.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Ap;

/// <summary>
/// EF Core mapping for a vendor invoice.
/// </summary>
public sealed class VendorInvoiceConfiguration : IEntityTypeConfiguration<VendorInvoice>
{
    public void Configure(EntityTypeBuilder<VendorInvoice> b)
    {
        b.ToTable("VendorInvoices", "ap");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasMaxLength(50);
        // The domain computes NormalizedInvoiceNumber on the fly; a computed column is stored for the unique index.
        // UPPER(TRIM(...)) trims only spaces, string.Trim() trims any whitespace: the primary duplicate check
        // is in the domain and the assembler, the index is the last guard against a race.
        b.Ignore(x => x.NormalizedInvoiceNumber);
        b.Property<string>("NormalizedNumber").IsRequired().HasMaxLength(50).HasComputedColumnSql("UPPER(TRIM([Number]))", stored: true);
        b.HasIndex("VendorId", "NormalizedNumber").IsUnique().HasDatabaseName("UX_VendorInvoices_Vendor_Number");
        b.Property(x => x.Total).HasConversion(Conversions.Money).HasPrecision(18, 2);
        b.Property(x => x.PoRef).HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.CreatedBy).HasConversion(Conversions.User);
        b.Property(x => x.RuleFingerprint).HasMaxLength(64);
        b.Property(x => x.ChangeStamp);
        b.Property<byte[]>("RowVersion").IsRowVersion();
        b.Property(x => x.Reference).HasMaxLength(40);
        b.HasIndex(x => x.Reference).IsUnique();
        b.Ignore(x => x.IsPoBacked).Ignore(x => x.DistributedTotal);
        b.PrimitiveCollection(x => x.ReservationRefs);
        b.PrimitiveCollection(x => x.EncumbranceClaimRefs);
        b.PrimitiveCollection(x => x.PoBillingClaimRefs);
        b.Property<IReadOnlyList<ApprovalRequirement>>("_requirements").HasColumnName("ApprovalRoute").AsJson();

        // RemoveDistribution renumbers lines via with: EF deletes the old owned rows and inserts new ones.
        b.OwnsMany(x => x.Distributions, o =>
        {
            o.ToTable("InvoiceDistributions", "ap");
            o.WithOwner().HasForeignKey("InvoiceId");
            o.Property<int>("Id").ValueGeneratedOnAdd();
            o.HasKey("Id");
            o.Property(d => d.Account).HasConversion(Conversions.Account).HasMaxLength(64);
            o.Property(d => d.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
        });
        b.Navigation(x => x.Distributions).HasField("_distributions").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Approvals, o =>
        {
            o.ToTable("InvoiceApprovals", "ap");
            o.WithOwner().HasForeignKey("InvoiceId");
            o.Property<int>("Id").ValueGeneratedOnAdd();
            o.HasKey("Id");
            o.Property(a => a.Role).HasConversion<string>().HasMaxLength(30);
            // DepartmentCode?: EF does not pass null to the converter, `!` only removes the nullability difference of the generic argument.
            o.Property(a => a.Department).HasConversion(Conversions.Department!).HasMaxLength(4);
            o.Property(a => a.UserId).HasConversion(Conversions.User);
            o.Property(a => a.Decision).HasConversion<string>().HasMaxLength(10);
            o.Property(a => a.RuleFingerprint).HasMaxLength(64);
            o.Property(a => a.Reason).HasMaxLength(1000);
        });
        b.Navigation(x => x.Approvals).HasField("_approvals").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Overrides, o =>
        {
            o.ToTable("InvoiceOverrides", "ap");
            o.WithOwner().HasForeignKey("InvoiceId");
            o.Property<int>("Id").ValueGeneratedOnAdd();
            o.HasKey("Id");
            // Positional record InvoiceOverride: EF binds the constructor only to scalars, so Target is a JSON column.
            o.Property(v => v.Target).AsJson();
            o.Property(v => v.Role).HasConversion<string>().HasMaxLength(30);
            o.Property(v => v.UserId).HasConversion(Conversions.User);
            o.Property(v => v.Reason).HasMaxLength(1000);
        });
        b.Navigation(x => x.Overrides).HasField("_overrides").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Withdrawals, o =>
        {
            o.ToTable("InvoiceWithdrawals", "ap");
            o.WithOwner().HasForeignKey("InvoiceId");
            o.Property<int>("Id").ValueGeneratedOnAdd();
            o.HasKey("Id");
            o.Property(w => w.UserId).HasConversion(Conversions.User);
            o.Property(w => w.Reason).HasMaxLength(1000);
        });
        b.Navigation(x => x.Withdrawals).HasField("_withdrawals").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
