using GovErp.Domain.ChartOfAccounts.Exceptions;

namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>Заведённый и утверждённый полный адрес счёта (whitelist) с effective dating.</summary>
public sealed class AccountCombination
{
    public Guid Id { get; private set; }
    public AccountCode Code { get; private set; }
    public CombinationStatus Status { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public CombinationSource Source { get; private set; }
    public UserId CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public UserId? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    private AccountCombination(Guid id, AccountCode code, DateOnly effectiveFrom, UserId createdBy,
        DateTimeOffset createdAt, CombinationSource source)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (createdBy.Value == Guid.Empty)
        {
            throw new ArgumentException("Requester is required.", nameof(createdBy));
        }
        Id = id;
        Code = code;
        Status = CombinationStatus.Pending;
        EffectiveFrom = effectiveFrom;
        Source = source;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    private AccountCombination() { Code = null!; }

    public static AccountCombination Request(AccountCode code, DateOnly effectiveFrom, UserId requestedBy,
        DateTimeOffset at, CombinationSource source) =>
        new(Guid.NewGuid(), code, effectiveFrom, requestedBy, at, source);

    public void Approve(UserId approvedBy, DateTimeOffset at)
    {
        if (approvedBy.Value == Guid.Empty)
        {
            throw new ArgumentException("Approver is required.", nameof(approvedBy));
        }
        if (Status != CombinationStatus.Pending)
        {
            throw new ChartOfAccountsException($"Combination {Code} is {Status}; only Pending can be approved.");
        }

        Status = CombinationStatus.Active;
        ApprovedBy = approvedBy;
        ApprovedAt = at;
    }

    public void Deactivate(DateOnly effectiveTo)
    {
        if (Status != CombinationStatus.Active)
        {
            throw new ChartOfAccountsException($"Combination {Code} is {Status}; only Active can be deactivated.");
        }

        if (effectiveTo < EffectiveFrom)
        {
            throw new ChartOfAccountsException($"EffectiveTo {effectiveTo} is before EffectiveFrom {EffectiveFrom}.");
        }

        Status = CombinationStatus.Inactive;
        EffectiveTo = effectiveTo;
    }

    public bool IsActiveOn(DateOnly date) =>
        Status != CombinationStatus.Pending
        && date >= EffectiveFrom
        && (EffectiveTo is null || date <= EffectiveTo);
}
