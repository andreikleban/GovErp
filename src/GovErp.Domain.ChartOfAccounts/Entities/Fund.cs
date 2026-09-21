namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>Фонд — самостоятельная учётная единица с ограничениями на использование. Справочник (анемичный), кроме Check.</summary>
public sealed class Fund
{
    public FundCode Code { get; private set; }
    public string Name { get; private set; }
    public FundType Type { get; private set; }
    public AccountingBasis Basis { get; private set; }
    public BudgetControlMode ControlMode { get; private set; }
    public GrantPolicy GrantPolicy { get; private set; }
    /// <summary>Пусто — без ограничений.</summary>
    public IReadOnlyList<DepartmentCode> AllowedDepartments { get; private set; }
    /// <summary>Пусто — без ограничений.</summary>
    public IReadOnlyList<ObjectCode> AllowedObjects { get; private set; }
    public bool IsActive { get; private set; }

    public Fund(FundCode code, string name, FundType type, AccountingBasis basis, BudgetControlMode controlMode,
        GrantPolicy grantPolicy, IReadOnlyList<DepartmentCode> allowedDepartments, IReadOnlyList<ObjectCode> allowedObjects,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(allowedDepartments);
        if (allowedDepartments.Any(item => item is null))
        {
            throw new ArgumentException("Departments cannot contain null.", nameof(allowedDepartments));
        }
        Code = code;
        Name = name;
        Type = type;
        Basis = basis;
        ControlMode = controlMode;
        GrantPolicy = grantPolicy;
        AllowedDepartments = Array.AsReadOnly(allowedDepartments.ToArray());
        ArgumentNullException.ThrowIfNull(allowedObjects);
        if (allowedObjects.Any(item => item is null))
        {
            throw new ArgumentException("Objects cannot contain null.", nameof(allowedObjects));
        }
        AllowedObjects = Array.AsReadOnly(allowedObjects.ToArray());
        IsActive = isActive;
    }

    /// <summary>Для восстановления из хранилища (DDD-9).</summary>
    private Fund()
    {
        Code = null!;
        Name = null!;
        AllowedDepartments = [];
        AllowedObjects = [];
    }

    public FundRestrictionCheck Check(DepartmentCode department, ObjectCode objectCode)
    {
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(objectCode);
        if (AllowedDepartments.Count > 0 && !AllowedDepartments.Contains(department))
        {
            return FundRestrictionCheck.DepartmentNotAllowed;
        }

        if (AllowedObjects.Count > 0 && !AllowedObjects.Contains(objectCode))
        {
            return FundRestrictionCheck.ObjectNotAllowed;
        }

        return FundRestrictionCheck.Allowed;
    }
}
