namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// The set of lines a posting would create.
/// </summary>
public sealed class PostingPreview
{
    public IReadOnlyList<PostingPreviewLine> Lines { get; }
    public IReadOnlyList<Problem> Failures { get; }

    public PostingPreview(IReadOnlyList<PostingPreviewLine> lines, IReadOnlyList<Problem>? failures = null)
    {
        Lines = Array.AsReadOnly(lines.ToArray());
        Failures = Array.AsReadOnly(failures?.ToArray() ?? []);
    }
}
