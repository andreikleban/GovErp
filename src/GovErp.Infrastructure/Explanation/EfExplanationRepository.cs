using GovErp.Application.Web.Explanation;
using GovErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Explanation;

/// <summary>
/// Stores explanation texts in the tenant database.
/// </summary>
public sealed class EfExplanationRepository(GovErpDbContext db) : IExplanationRepository
{
    public void Add(ExplanationRecord record) => db.Explanations.Add(record);

    public async Task<IReadOnlyList<ExplanationRecord>> ListByEvaluationAsync(Guid evaluationId, CancellationToken ct = default) =>
        await db.Explanations.Where(e => e.EvaluationId == evaluationId).OrderBy(e => e.GeneratedAt).ToListAsync(ct);
}
