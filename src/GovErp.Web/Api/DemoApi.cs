using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Identity;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Ledger;
using GovErp.Application.Web.Ledger.Contracts;
using GovErp.Application.Web.Posting;
using GovErp.Infrastructure.Operations;
using GovErp.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GovErp.Web.Api;

/// <summary>
/// A small HTTP API over the same application services and cookie as the screens. It demonstrates a second consumer; it is not an integration API.
/// </summary>
public static class DemoApi
{
    public static IEndpointRouteBuilder MapDemoApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").DisableAntiforgery();
        api.MapPost("/session", Session).AllowAnonymous()
            .Produces<SessionResponse>().Produces(StatusCodes.Status401Unauthorized);
        api.MapGet("/invoices/{id:guid}", GetInvoice)
            .Produces<InvoiceVm>().Produces<ApiFailure>(StatusCodes.Status404NotFound);
        api.MapGet("/funds/{code}/report", FundReport)
            .Produces<FundReportVm>().Produces<ApiFailure>(StatusCodes.Status404NotFound);
        api.MapPost("/invoices/{id:guid}/payments", Pay)
            .Produces<InvoiceVm>()
            .Produces<ApiFailure>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ApiFailure>(StatusCodes.Status403Forbidden)
            .Produces<ApiFailure>(StatusCodes.Status404NotFound)
            .Produces<ApiFailure>(StatusCodes.Status409Conflict);
        api.MapGet("/observability", Observability).Produces<IReadOnlyList<CommandCount>>();
        return app;
    }

    private static async Task<IResult> Session(SessionRequest body, ISignIn signIn, HttpContext http)
    {
        var actor = await signIn.AuthenticateAsync(body.UserName, body.Password);
        if (actor is null)
        {
            return TypedResults.Unauthorized();
        }

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ActorClaims.ToPrincipal(actor));
        return TypedResults.Ok(new SessionResponse(actor.UserName, actor.TenantKey));
    }

    private static async Task<IResult> GetInvoice(Guid id, HttpContext http, IInvoiceAppService invoices, CancellationToken ct)
    {
        try
        {
            return TypedResults.Ok(await invoices.GetAsync(id, ActorClaims.ToActor(http.User), ct));
        }
        catch (NotFoundException ex)
        {
            return TypedResults.NotFound(new ApiFailure(ex.Code, ex.Reason));
        }
    }

    private static async Task<IResult> FundReport(string code, HttpContext http, ILedgerAppService ledger, CancellationToken ct)
    {
        var rows = await ledger.ListFundReportsAsync(ActorClaims.ToActor(http.User), ct);
        var row = rows.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
        return row is null
            ? TypedResults.NotFound(new ApiFailure(AppErrors.FundNotFound, Messages.Render(AppErrors.FundNotFound, new Dictionary<string, string> { ["fund"] = code })))
            : TypedResults.Ok(row);
    }

    private static async Task<IResult> Pay(
        Guid id, PayRequest body, HttpContext http, IPostingAppService posting, CancellationToken ct)
    {
        var result = await posting.PayAsync(new InvoiceActionCommand(new CommandEnvelope(body.CommandId, body.ExpectedRowVersion), id), ActorClaims.ToActor(http.User), ct);
        return Map(result);
    }

    private static IResult Map(CommandResult<InvoiceVm> result) =>
        result.Status switch
        {
            CommandStatus.Accepted => TypedResults.Ok(result.Value!),
            CommandStatus.Refused => TypedResults.UnprocessableEntity(ApiFailure.Of(result)),
            CommandStatus.Forbidden => TypedResults.Json(ApiFailure.Of(result), statusCode: StatusCodes.Status403Forbidden),
            CommandStatus.NotFound => TypedResults.NotFound(ApiFailure.Of(result)),
            _ => TypedResults.Conflict(ApiFailure.Of(result)),
        };

    private static IResult Observability() =>
        TypedResults.Ok<IReadOnlyList<CommandCount>>(CommandMetrics.Snapshot()
            .Select(row => new CommandCount(row.Command, row.Status, row.Count)).ToList());
}

/// <summary>Login body for POST /api/session.</summary>
public sealed record SessionRequest(string UserName, string Password);

/// <summary>Who the cookie now represents.</summary>
public sealed record SessionResponse(string UserName, string Tenant);

/// <summary>Payment command. CommandId makes a repeat of the same call return the stored result.</summary>
public sealed record PayRequest(Guid CommandId, string? ExpectedRowVersion);

/// <summary>A refused, missing or conflicting command.</summary>
public sealed record ApiFailure(string? Code, string? Reason, object? Value = null)
{
    public static ApiFailure Of<T>(CommandResult<T> result) => new(result.Code, result.Reason, result.Value);
}

/// <summary>How many times a command finished with a status in this process.</summary>
public sealed record CommandCount(string Command, string Status, long Count);
