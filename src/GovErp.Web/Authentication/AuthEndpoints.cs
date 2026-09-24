using GovErp.Application.Web.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GovErp.Web.Authentication;

/// <summary>
/// HTTP routes for login and logout.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async Task<IResult> (HttpContext http, ISignIn signIn, IAntiforgery antiforgery) =>
            await LoginAsync(http, signIn, antiforgery)).AllowAnonymous();
        app.MapPost("/auth/logout", async Task<IResult> (HttpContext http, IAntiforgery antiforgery) =>
            await LogoutAsync(http, antiforgery)).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> LoginAsync(HttpContext http, ISignIn signIn, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }
        var form = await http.Request.ReadFormAsync();
        var actor = await signIn.AuthenticateAsync(form["username"].ToString(), form["password"].ToString());
        if (actor is null)
        {
            return Results.Redirect("/login?error=1");
        }

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ActorClaims.ToPrincipal(actor));
        return Results.Redirect(LocalUrl(form["returnUrl"].ToString()));
    }

    private static async Task<IResult> LogoutAsync(HttpContext http, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }

    internal static string LocalUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//")
            || returnUrl.Contains('\\', StringComparison.Ordinal) || returnUrl.Contains("://", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }
}
