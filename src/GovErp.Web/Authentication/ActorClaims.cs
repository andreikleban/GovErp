using System.Security.Claims;
using GovErp.Application.Web.Common;

namespace GovErp.Web.Authentication;

/// <summary>
/// The only place where ActorContext becomes cookie claims and back (GE-5).
/// </summary>
public static class ActorClaims
{
    public const string Tenant = "tenant";
    public const string Sub = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string Dept = "dept";

    public static ClaimsPrincipal ToPrincipal(ActorContext actor)
    {
        var claims = new List<Claim>
        {
            new(Tenant, actor.TenantKey),
            new(Sub, actor.UserKey.ToString()),
            new(Name, actor.UserName),
        };
        foreach (var role in actor.Roles)
        {
            claims.Add(new(Role, role));
        }

        if (actor.DepartmentCode is { Length: > 0 } dept)
        {
            claims.Add(new(Dept, dept));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies", Name, Role));
    }

    public static ActorContext ToActor(ClaimsPrincipal principal)
    {
        var tenant = principal.FindFirst(Tenant)?.Value
            ?? throw new InvalidOperationException("Authenticated principal is missing tenant.");
        var sub = principal.FindFirst(Sub)?.Value
            ?? throw new InvalidOperationException("Authenticated principal is missing sub.");
        var name = principal.FindFirst(Name)?.Value
            ?? throw new InvalidOperationException("Authenticated principal is missing name.");
        var roles = principal.FindAll(Role).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        return ActorContext.Create(tenant, Guid.Parse(sub), name, roles, principal.FindFirst(Dept)?.Value);
    }
}
