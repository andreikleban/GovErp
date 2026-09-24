namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// The posting problems, or Passed when there are none.
/// </summary>
public sealed class PostingCheck
{
    public bool Passed { get; }
    public IReadOnlyList<Problem> Failures { get; }

    public PostingCheck(bool passed, IReadOnlyList<Problem> failures)
    {
        Failures = Array.AsReadOnly(failures.ToArray());
        Passed = passed && Failures.Count == 0;
    }

    public static PostingCheck Ok { get; } = new(true, []);
}
