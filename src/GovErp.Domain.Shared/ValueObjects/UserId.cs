namespace GovErp.Domain.Shared.ValueObjects;

public readonly record struct UserId
{
    public Guid Value { get; }
    public UserId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("User identifier cannot be empty.", nameof(value));
        Value = value;
    }
    public static UserId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
