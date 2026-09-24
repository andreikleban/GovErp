using GovErp.Domain.ChartOfAccounts.Exceptions;

namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>
/// A fund is a self-balancing accounting entity with restrictions on its use. Reference data (anemic), except Check.
/// </summary>
public sealed class Fund
{
    public FundCode Code { get; private set; }
    public string Name { get; private set; }
    public FundType Type { get; private set; }
    public AccountingBasis Basis { get; private set; }
    public BudgetControlMode ControlMode { get; private set; }
    public GrantPolicy GrantPolicy { get; private set; }
    /// <summary>
    /// Empty means no restriction.
    /// </summary>
    public IReadOnlyList<DepartmentCode> AllowedDepartments { get; private set; }
    /// <summary>
    /// Empty means no restriction.
    /// </summary>
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
            throw new InvalidValueException(nameof(allowedDepartments), ChartOfAccountsErrors.NullDepartment);
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
            throw new InvalidValueException(nameof(allowedObjects), ChartOfAccountsErrors.NullObject);
        }
        AllowedObjects = Array.AsReadOnly(allowedObjects.ToArray());
        IsActive = isActive;
    }

    /// <summary>
    /// For rehydration from storage (DDD-9).
    /// </summary>
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
