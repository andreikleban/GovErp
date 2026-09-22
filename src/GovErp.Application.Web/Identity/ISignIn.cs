using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Identity;

public interface ISignIn
{
    Task<ActorContext?> AuthenticateAsync(string userName, string password, CancellationToken ct = default);
}
