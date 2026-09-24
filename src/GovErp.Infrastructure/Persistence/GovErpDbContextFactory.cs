using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GovErp.Infrastructure.Persistence;

/// <summary>
/// For dotnet ef only: the migration generator needs a connection string, no connection is opened.
/// </summary>
public sealed class GovErpDbContextFactory : IDesignTimeDbContextFactory<GovErpDbContext>
{
    public GovErpDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GovErpDbContext>()
            .UseSqlServer("Server=localhost;Database=GovErp_Design;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
