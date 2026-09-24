using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GovErp.Infrastructure.Master;

/// <summary>
/// For dotnet ef only: the migration generator needs a connection string, no connection is opened.
/// </summary>
public sealed class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterDbContext>
{
    public MasterDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer("Server=localhost;Database=GovErp_Design;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
