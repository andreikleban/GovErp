using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

public sealed class EfRuleDefinitionRepository(GovErpDbContext db) : IRuleDefinitionRepository
{
    /// <summary>Все версии и слои, включая выключенные: разрешение делает домен.</summary>
    public async Task<IReadOnlyList<RuleDefinition>> ListAsync(CancellationToken ct = default) =>
        await db.RuleDefinitions.OrderBy(r => r.RuleId).ThenBy(r => r.Layer).ThenBy(r => r.Version).ToListAsync(ct);
}
