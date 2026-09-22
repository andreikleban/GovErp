using GovErp.Application.Web.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Ap;

public sealed class CommandReceiptConfiguration : IEntityTypeConfiguration<CommandReceipt>
{
    public void Configure(EntityTypeBuilder<CommandReceipt> b)
    {
        b.ToTable("ProcessedCommands", "ap");
        b.HasKey(x => x.CommandId);
        b.Property(x => x.CommandId).ValueGeneratedNever();
        b.Property(x => x.ActorId).HasConversion(Conversions.User);
        b.Property(x => x.RequestHash).HasMaxLength(64);
        b.Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
    }
}
