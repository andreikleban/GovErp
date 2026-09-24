using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.Repositories;

public interface IRuleDefinitionRepository
{
    /// <summary>Returns all versions and layers, including disabled definitions, for domain resolution.</summary>
    Task<IReadOnlyList<RuleDefinition>> ListAsync(CancellationToken ct = default);

    /// <summary>A new rule version. Existing versions never change: past evaluations refer to them.</summary>
    Task AddAsync(RuleDefinition rule, CancellationToken ct = default);
}
