namespace GovErp.Domain.Shared.ValueObjects;

public sealed record FundCode : SegmentCode
{
    public FundCode(string value) : base(value, @"\A[0-9]{3}\z", "Fund") { }
}
