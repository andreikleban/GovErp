using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Identity;

/// <summary>
/// Checks a login and password against the user catalog.
/// </summary>
public interface ISignIn
{
    Task<ActorContext?> AuthenticateAsync(string userName, string password, CancellationToken ct = default);
}
