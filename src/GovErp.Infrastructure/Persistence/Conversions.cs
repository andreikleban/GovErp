using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GovErp.Infrastructure.Persistence;

/// <summary>
/// Converts value objects to and from database columns.
/// </summary>
internal static class Conversions
{
    public static readonly ValueConverter<Money, decimal> Money = new(m => m.Amount, d => new Money(d));
    public static readonly ValueConverter<FundCode, string> Fund = new(c => c.Value, s => new FundCode(s));
    public static readonly ValueConverter<DepartmentCode, string> Department = new(c => c.Value, s => new DepartmentCode(s));
    public static readonly ValueConverter<ObjectCode, string> Object = new(c => c.Value, s => new ObjectCode(s));
    public static readonly ValueConverter<GrantCode, string> Grant = new(c => c.Value, s => new GrantCode(s));
    public static readonly ValueConverter<AccountCode, string> Account = new(c => c.ToString(), s => AccountCode.Parse(s));
    public static readonly ValueConverter<FiscalYear, int> FiscalYear = new(f => f.Year, y => new FiscalYear(y));
    public static readonly ValueConverter<UserId, Guid> User = new(u => u.Value, g => new UserId(g));
    public static readonly ValueConverter<TenantId, string> Tenant = new(t => t.Value, s => new TenantId(s));
}
