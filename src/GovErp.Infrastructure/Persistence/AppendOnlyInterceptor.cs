using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Explanation;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Validation.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GovErp.Infrastructure.Persistence;

/// <summary>Evaluations, explanations, journals and audit are INSERT-only (GE-12). The second line of defence is DENY UPDATE, DELETE for the runtime database user.</summary>
public sealed class AppendOnlyInterceptor : SaveChangesInterceptor
{
    private static readonly Type[] AppendOnly = [typeof(EvaluationRecord), typeof(ExplanationRecord), typeof(JournalEntry), typeof(AuditEvent)];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context!);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Check(eventData.Context!);
        return ValueTask.FromResult(result);
    }

    private static void Check(DbContext db)
    {
        var offender = db.ChangeTracker.Entries()
            .FirstOrDefault(x => AppendOnly.Contains(x.Entity.GetType()) && x.State is EntityState.Modified or EntityState.Deleted);
        if (offender is not null)
        {
            throw new InvalidOperationException($"{offender.Entity.GetType().Name} is append-only (GE-12).");
        }
    }
}
