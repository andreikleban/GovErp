using GovErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Master;

/// <summary>
/// EF context for the tenant catalog and user accounts.
/// </summary>
public sealed class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(t => { t.ToTable("Tenants"); t.HasKey(x => x.Id); t.Property(x => x.Id).HasMaxLength(50); });
        b.Entity<UserAccount>(u =>
        {
            u.ToTable("Users");
            u.HasKey(x => x.Id);
            u.Property(x => x.UserName).HasMaxLength(256);
            u.HasIndex(x => x.UserName).IsUnique();
            u.Property(x => x.Roles).AsJson();
        });
    }
}
