namespace GovErp.Domain.Shared.ValueObjects;

public sealed record ObjectCode : SegmentCode
{
    public ObjectCode(string value) : base(value, @"\A[0-9]{4,5}\z", "Object") { }
}
