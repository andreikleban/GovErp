using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GovErp.Infrastructure.Persistence;

/// <summary>Только для dotnet ef: строка подключения нужна генератору миграций, соединение не открывается.</summary>
public sealed class GovErpDbContextFactory : IDesignTimeDbContextFactory<GovErpDbContext>
{
    public GovErpDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GovErpDbContext>()
            .UseSqlServer("Server=localhost;Database=GovErp_Design;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
