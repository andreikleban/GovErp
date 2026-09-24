namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>
/// Identifier of a user.
/// </summary>
public readonly record struct UserId
{
    public Guid Value { get; }
    public UserId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidValueException(nameof(value), ValueErrors.EmptyUser);
        Value = value;
    }
    public static UserId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
