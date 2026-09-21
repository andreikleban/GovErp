namespace GovErp.Domain.ChartOfAccounts.Entities;

public sealed class Department
{
    public DepartmentCode Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    public Department(DepartmentCode code, string name, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(code);
        Code = code;
        Name = name;
        IsActive = isActive;
    }

    private Department() { Code = null!; Name = null!; }
}
