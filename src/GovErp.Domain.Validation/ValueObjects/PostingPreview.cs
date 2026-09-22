namespace GovErp.Domain.Validation.ValueObjects;

public sealed class PostingPreview
{
    public IReadOnlyList<PostingPreviewLine> Lines { get; }
    public IReadOnlyList<string> Failures { get; }

    public PostingPreview(IReadOnlyList<PostingPreviewLine> lines, IReadOnlyList<string>? failures = null)
    {
        Lines = Array.AsReadOnly(lines.ToArray());
        Failures = Array.AsReadOnly(failures?.ToArray() ?? []);
    }
}
