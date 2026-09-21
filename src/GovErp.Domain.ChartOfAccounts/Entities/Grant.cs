namespace GovErp.Domain.ChartOfAccounts.Entities;

public sealed class Grant
{
    public GrantCode Code { get; private set; }
    public string Name { get; private set; }
    public string Sponsor { get; private set; }
    public bool IsFederal { get; private set; }
    public DatePeriod Period { get; private set; }
    /// <summary>Пусто — без ограничений.</summary>
    public IReadOnlyList<DepartmentCode> AllowedDepartments { get; private set; }
    /// <summary>Пусто — без ограничений.</summary>
    public IReadOnlyList<ObjectCode> AllowableObjects { get; private set; }
    public GrantStatus Status { get; private set; }

    public Grant(GrantCode code, string name, string sponsor, bool isFederal, DatePeriod period,
        IReadOnlyList<DepartmentCode> allowedDepartments, IReadOnlyList<ObjectCode> allowableObjects,
        GrantStatus status = GrantStatus.Active)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sponsor);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(allowedDepartments);
        if (allowedDepartments.Any(item => item is null))
        {
            throw new ArgumentException("Departments cannot contain null.", nameof(allowedDepartments));
        }
        Code = code;
        Name = name;
        Sponsor = sponsor;
        IsFederal = isFederal;
        ArgumentNullException.ThrowIfNull(period);
        Period = period;
        AllowedDepartments = Array.AsReadOnly(allowedDepartments.ToArray());
        ArgumentNullException.ThrowIfNull(allowableObjects);
        if (allowableObjects.Any(item => item is null))
        {
            throw new ArgumentException("Objects cannot contain null.", nameof(allowableObjects));
        }
        AllowableObjects = Array.AsReadOnly(allowableObjects.ToArray());
        Status = status;
    }

    private Grant()
    {
        Code = null!;
        Name = null!;
        Sponsor = null!;
        Period = null!;
        AllowedDepartments = [];
        AllowableObjects = [];
    }

    public GrantEligibility CheckEligibility(DateOnly transactionDate, DepartmentCode department, ObjectCode objectCode)
    {
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(objectCode);
        if (Status != GrantStatus.Active)
        {
            return GrantEligibility.GrantNotActive;
        }

        if (!Period.Contains(transactionDate))
        {
            return GrantEligibility.OutsidePeriod;
        }

        if (AllowedDepartments.Count > 0 && !AllowedDepartments.Contains(department))
        {
            return GrantEligibility.DepartmentNotAllowed;
        }

        if (AllowableObjects.Count > 0 && !AllowableObjects.Contains(objectCode))
        {
            return GrantEligibility.ObjectNotAllowed;
        }

        return GrantEligibility.Eligible;
    }
}
