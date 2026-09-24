namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>
/// Object segment of an account code.
/// </summary>
public sealed record ObjectCode : SegmentCode
{
    public ObjectCode(string value) : base(value, @"\A[0-9]{4,5}\z", "Object") { }
}
