using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.Repositories;

public interface IRuleDefinitionRepository
{
    /// <summary>Returns all versions and layers, including disabled definitions, for domain resolution.</summary>
    Task<IReadOnlyList<RuleDefinition>> ListAsync(CancellationToken ct = default);

    /// <summary>Новая версия правила. Существующие версии не меняются: прошлые оценки ссылаются на них.</summary>
    Task AddAsync(RuleDefinition rule, CancellationToken ct = default);
}
