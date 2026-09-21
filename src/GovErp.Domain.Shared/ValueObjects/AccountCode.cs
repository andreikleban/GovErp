namespace GovErp.Domain.Shared.ValueObjects;

public sealed record AccountCode
{
    public FundCode Fund { get; }
    public DepartmentCode Department { get; }
    public ObjectCode Object { get; }
    public GrantCode? Grant { get; }

    public AccountCode(FundCode fund, DepartmentCode department, ObjectCode @object, GrantCode? grant)
    {
        ArgumentNullException.ThrowIfNull(fund);
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(@object);
        Fund = fund;
        Department = department;
        Object = @object;
        Grant = grant;
    }

    public static AccountCode Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parts = text.Split('-', 4);
        if (parts.Length < 3) throw new ArgumentException("An account requires fund, department and object.", nameof(text));
        return new(new(parts[0]), new(parts[1]), new(parts[2]), parts.Length == 4 ? new(parts[3]) : null);
    }

    public AccountCode WithObject(ObjectCode objectCode) => new(Fund, Department, objectCode, Grant);
    public override string ToString() => Grant is null ? $"{Fund}-{Department}-{Object}" : $"{Fund}-{Department}-{Object}-{Grant}";
}
