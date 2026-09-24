namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// What an evaluation allows: save, submit, approve, post and pay.
/// </summary>
public sealed record Capabilities(bool CanSave, bool CanSubmit, bool CanApprove, bool CanPost, bool CanPay)
{
    public static Capabilities For(Severity overall, bool? postingPassed) => overall switch
    {
        Severity.HardStop => new(true, false, false, false, false),
        Severity.SoftStop => new(true, true, false, false, false),
        _ => new(true, true, true, postingPassed == true, false),
    };

    /// <summary>
    /// What the result allows, limited to the one action the document status leads to.
    /// </summary>
    public static Capabilities ForDocument(string status, Severity overall, bool? postingPassed)
    {
        var byResult = For(overall, postingPassed);
        return status switch
        {
            "Draft" => new(true, byResult.CanSubmit, false, false, false),
            "Submitted" => new(false, false, byResult.CanApprove, false, false),
            "Approved" => new(false, false, false, byResult.CanPost, false),
            _ => new(false, false, false, false, false),
        };
    }
}
