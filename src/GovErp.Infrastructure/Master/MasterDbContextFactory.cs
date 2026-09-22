using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GovErp.Infrastructure.Master;

/// <summary>Только для dotnet ef: строка подключения нужна генератору миграций, соединение не открывается.</summary>
public sealed class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterDbContext>
{
    public MasterDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer("Server=localhost;Database=GovErp_Design;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
