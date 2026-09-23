using System.Data.Common;

namespace GovErp.Web.Extensions;

/// <summary>
/// Aspire кладёт SA-строку в ConnectionStrings:sql. От неё собираются шаблоны миграции,
/// runtime-тенантов и Master — несколько БД на одном экземпляре (GE-6).
/// </summary>
public static class AspireSqlConfiguration
{
    public const string ResourceName = "sql";

    public static void Apply(ConfigurationManager configuration)
    {
        var sql = configuration.GetConnectionString(ResourceName);
        if (string.IsNullOrWhiteSpace(sql))
        {
            return;
        }

        var parsed = new DbConnectionStringBuilder { ConnectionString = sql };
        var server = Required(parsed, "Server", "Data Source");
        var user = Required(parsed, "User ID", "User Id", "UID");
        var password = Required(parsed, "Password", "Pwd");
        var masterPassword = configuration["Startup:MasterRuntimePassword"] ?? "1!Qwertyui";

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Startup:MigrationConnectionTemplate"] = ToFormatTemplate(
                server, database: "{0}", user, password),
            ["Tenancy:RuntimeConnectionTemplate"] = ToFormatTemplate(
                server, database: "{0}", user: "{1}", password: "{2}"),
            ["ConnectionStrings:Master"] = Build(
                server, "GovErp_Master", "goverp_master_ro", masterPassword),
        });
    }

    /// <summary>
    /// Aspire генерирует SA-пароль со скобками. Их нельзя класть в шаблон <c>string.Format</c> как есть.
    /// </summary>
    private static string ToFormatTemplate(string server, string database, string user, string password)
    {
        return EscapeForCompositeFormat(Build(server, database, user, password))
            .Replace("{{0}}", "{0}", StringComparison.Ordinal)
            .Replace("{{1}}", "{1}", StringComparison.Ordinal)
            .Replace("{{2}}", "{2}", StringComparison.Ordinal);
    }

    private static string Build(string server, string database, string user, string password) =>
        new DbConnectionStringBuilder
        {
            ["Server"] = server,
            ["Database"] = database,
            ["User Id"] = user,
            ["Password"] = password,
            ["TrustServerCertificate"] = true,
        }.ConnectionString;

    private static string EscapeForCompositeFormat(string value) =>
        value.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);

    private static string Required(DbConnectionStringBuilder parsed, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (parsed.ContainsKey(key))
            {
                var text = Convert.ToString(parsed[key]);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        throw new InvalidOperationException($"Aspire SQL connection is missing {string.Join("/", keys)}.");
    }
}
