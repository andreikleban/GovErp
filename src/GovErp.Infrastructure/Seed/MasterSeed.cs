using GovErp.Application.Web.Common;
using GovErp.Infrastructure.Master;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Seed;

/// <summary>Демо-пользователи Master (spec §2). Тенантов создаёт TenantProvisioner, не этот класс.</summary>
public static class MasterSeed
{
    public const string DemoPassword = "1!Qwertyui";

    private const string Springfield = "springfield";
    private const string Shelbyville = "shelbyville";

    // ap.clerk использует SpringfieldData.ClerkId — тот же пользователь фигурирует в демо-инвойсах (spec §2.4).
    public static readonly Guid FireChiefId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid PoliceChiefId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid PwDirectorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid WaterDirectorId = Guid.Parse("10000000-0000-0000-0000-000000000005");
    public static readonly Guid GrantsManagerId = Guid.Parse("10000000-0000-0000-0000-000000000006");
    public static readonly Guid BudgetOfficerId = Guid.Parse("10000000-0000-0000-0000-000000000007");
    public static readonly Guid FinanceDirectorId = Guid.Parse("10000000-0000-0000-0000-000000000008");
    private static readonly Guid ShelbyClerkId = Guid.Parse("10000000-0000-0000-0000-000000000009");
    private static readonly Guid ShelbyFinanceId = Guid.Parse("10000000-0000-0000-0000-00000000000a");

    private static IEnumerable<UserAccount> Candidates(IPasswordHasher<UserAccount> hasher)
    {
        UserAccount U(Guid id, string userName, string displayName, string tenantId, string[] roles, string? dept)
        {
            var user = new UserAccount
            {
                Id = id,
                UserName = userName,
                DisplayName = displayName,
                TenantId = tenantId,
                Roles = [.. roles],
                DepartmentCode = dept,
            };
            user.PasswordHash = hasher.HashPassword(user, DemoPassword);
            return user;
        }

        yield return U(SpringfieldData.ClerkId.Value, "ap.clerk", "AP Clerk", Springfield, [Roles.ApClerk], null);
        yield return U(FireChiefId, "fire.chief", "Fire Chief", Springfield, [Roles.DepartmentHead], "6000");
        yield return U(PoliceChiefId, "police.chief", "Police Chief", Springfield, [Roles.DepartmentHead], "3000");
        yield return U(PwDirectorId, "pw.director", "Public Works Director", Springfield, [Roles.DepartmentHead], "4000");
        yield return U(WaterDirectorId, "water.director", "Water Director", Springfield, [Roles.DepartmentHead], "5000");
        yield return U(GrantsManagerId, "grants.manager", "Grants Manager", Springfield, [Roles.GrantsManager], null);
        yield return U(BudgetOfficerId, "budget.officer", "Budget Officer", Springfield, [Roles.BudgetOfficer], null);
        yield return U(FinanceDirectorId, "finance.director", "Finance Director", Springfield, [Roles.FinanceDirector], null);
        yield return U(ShelbyClerkId, "shelby.clerk", "Shelbyville AP Clerk", Shelbyville, [Roles.ApClerk], null);
        yield return U(ShelbyFinanceId, "shelby.finance", "Shelbyville Finance Director", Shelbyville, [Roles.FinanceDirector], null);
    }

    /// <summary>
    /// Добавляет отсутствующих по UserName. Уже существующим выставляет общий демо-пароль,
    /// если сохранённый хеш ему не соответствует.
    /// </summary>
    public static async Task SeedUsersAsync(MasterDbContext master, IPasswordHasher<UserAccount> hasher, CancellationToken ct)
    {
        var existing = await master.Users.ToListAsync(ct);
        var names = existing.Select(u => u.UserName).ToHashSet(StringComparer.Ordinal);
        var missing = Candidates(hasher).Where(u => !names.Contains(u.UserName)).ToList();
        if (missing.Count > 0)
        {
            master.Users.AddRange(missing);
        }

        var changed = missing.Count > 0;
        foreach (var user in existing)
        {
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, DemoPassword) == PasswordVerificationResult.Failed)
            {
                user.PasswordHash = hasher.HashPassword(user, DemoPassword);
                changed = true;
            }
        }

        if (changed)
        {
            await master.SaveChangesAsync(ct);
        }
    }
}
