using GovErp.Application.Web.Common;
using GovErp.Application.Web.Identity;
using GovErp.Infrastructure.Master;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Identity;

public sealed class MasterSignIn(MasterDbContext master, IPasswordHasher<UserAccount> hasher) : ISignIn
{
    public async Task<ActorContext?> AuthenticateAsync(string userName, string password, CancellationToken ct = default)
    {
        var u = await master.Users.SingleOrDefaultAsync(x => x.UserName == userName, ct);
        if (u is null || hasher.VerifyHashedPassword(u, u.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return new ActorContext(new TenantId(u.TenantId), new UserId(u.Id), u.UserName, u.Roles.ToHashSet(), u.DepartmentCode);
    }
}
