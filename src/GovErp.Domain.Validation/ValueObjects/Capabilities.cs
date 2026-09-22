namespace GovErp.Domain.Validation.ValueObjects;

public sealed record Capabilities(bool CanSave, bool CanSubmit, bool CanApprove, bool CanPost, bool CanPay)
{
    public static Capabilities For(Severity overall, bool? postingPassed) => overall switch
    {
        Severity.HardStop => new(true, false, false, false, false),
        Severity.SoftStop => new(true, true, false, false, false),
        _ => new(true, true, true, postingPassed == true, false),
    };
}
