namespace GovErp.Application.Web.Commands;

/// <summary>Binds an aggregate to the RowVersion the user saw: a stale form yields Conflict instead of a silent overwrite.</summary>
public interface IConcurrencyGuard
{
    void Expect(object aggregate, string? rowVersion);
    string? VersionOf(object aggregate);
}
