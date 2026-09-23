using System.Globalization;
using GovErp.Application.Web.Invoices;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence;

/// <summary>
/// Счётчик в строке ap.DocumentCounters меняется в транзакции команды (runner уже открыл её на этом DbContext):
/// HOLDLOCK держит строку до commit, поэтому параллельные создания получают номера по очереди, а откат
/// команды возвращает номер — нумерация без пропусков.
/// </summary>
public sealed class EfInvoiceNumbering(GovErpDbContext db) : IInvoiceNumbering
{
    public async Task<int> NextAsync(FiscalYear fiscalYear, CancellationToken ct = default)
    {
        var series = Series(fiscalYear);
        var values = await db.Database.SqlQuery<int>($"""
            MERGE ap.DocumentCounters WITH (HOLDLOCK) AS t
            USING (SELECT {series} AS Series) AS s ON t.Series = s.Series
            WHEN MATCHED THEN UPDATE SET LastValue = t.LastValue + 1
            WHEN NOT MATCHED THEN INSERT (Series, LastValue) VALUES (s.Series, 1)
            OUTPUT inserted.LastValue AS Value;
            """).ToListAsync(ct);
        return values.Single();
    }

    public async Task<int> PeekAsync(FiscalYear fiscalYear, CancellationToken ct = default)
    {
        var series = Series(fiscalYear);
        var last = await db.DocumentCounters.Where(c => c.Series == series).Select(c => (int?)c.LastValue).SingleOrDefaultAsync(ct);
        return (last ?? 0) + 1;
    }

    private static string Series(FiscalYear fiscalYear) => string.Create(CultureInfo.InvariantCulture, $"AP-{fiscalYear.Year}");
}
