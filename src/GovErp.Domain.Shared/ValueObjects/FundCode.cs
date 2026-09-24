namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>
/// Fund segment of an account code.
/// </summary>
public sealed record FundCode : SegmentCode
{
    public FundCode(string value) : base(value, @"\A[0-9]{3}\z", "Fund") { }
}
