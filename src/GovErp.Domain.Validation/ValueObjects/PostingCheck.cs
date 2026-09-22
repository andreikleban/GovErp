namespace GovErp.Domain.Validation.ValueObjects;

public sealed class PostingCheck
{
    public bool Passed { get; }
    public IReadOnlyList<string> Failures { get; }

    public PostingCheck(bool passed, IReadOnlyList<string> failures)
    {
        Failures = Array.AsReadOnly(failures.ToArray());
        Passed = passed && Failures.Count == 0;
    }

    public static PostingCheck Ok { get; } = new(true, []);
}
