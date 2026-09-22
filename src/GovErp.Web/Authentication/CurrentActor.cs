using GovErp.Application.Web.Common;
using Microsoft.AspNetCore.Components.Authorization;

namespace GovErp.Web.Authentication;

/// <summary>Читает актора из cookie текущего запроса/circuit. Не кэшируется дольше вызова.</summary>
public sealed class CurrentActor(AuthenticationStateProvider authentication)
{
    public async Task<ActorContext> GetAsync()
    {
        var state = await authentication.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("No authenticated actor.");
        }

        return ActorClaims.ToActor(state.User);
    }
}
