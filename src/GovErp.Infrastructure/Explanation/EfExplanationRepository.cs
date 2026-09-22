using GovErp.Application.Web.Explanation;
using GovErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Explanation;

public sealed class EfExplanationRepository(GovErpDbContext db) : IExplanationRepository
{
    public void Add(ExplanationRecord record) => db.Explanations.Add(record);

    public async Task<IReadOnlyList<ExplanationRecord>> ListByEvaluationAsync(Guid evaluationId, CancellationToken ct = default) =>
        await db.Explanations.Where(e => e.EvaluationId == evaluationId).OrderBy(e => e.GeneratedAt).ToListAsync(ct);
}
