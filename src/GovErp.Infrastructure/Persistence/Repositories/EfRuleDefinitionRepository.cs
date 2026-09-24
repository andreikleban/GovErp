using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL repository of rule definitions for the current tenant.
/// </summary>
public sealed class EfRuleDefinitionRepository(GovErpDbContext db) : IRuleDefinitionRepository
{
    /// <summary>
    /// All versions and layers, including disabled ones: the domain does the resolution.
    /// </summary>
    public async Task<IReadOnlyList<RuleDefinition>> ListAsync(CancellationToken ct = default) =>
        await db.RuleDefinitions.OrderBy(r => r.RuleId).ThenBy(r => r.Layer).ThenBy(r => r.Version).ToListAsync(ct);

    public async Task AddAsync(RuleDefinition rule, CancellationToken ct = default) => await db.RuleDefinitions.AddAsync(rule, ct);
}
