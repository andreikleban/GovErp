using System.Net;
using System.Text.RegularExpressions;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Tests;
using GovErp.Infrastructure.Seed;
using GovErp.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GovErp.Web.Tests;

[Collection("sql")]
public sealed class AuthEndpointTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new WebFactory(fixture);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Anonymous_invoices_redirect_to_login()
    {
        var response = await _client.GetAsync("/invoices");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task Login_without_antiforgery_is_rejected()
    {
        var response = await _client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "fire.chief",
            ["password"] = MasterSeed.DemoPassword,
        }));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Cookie(response, "GovErp.Auth").Should().BeNull();
    }

    [Fact]
    public async Task Wrong_password_does_not_issue_cookie()
    {
        var response = await PostLoginAsync("fire.chief", "wrong");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/login");
        Cookie(response, "GovErp.Auth").Should().BeNull();
    }

    [Fact]
    public async Task Fire_chief_cookie_contains_springfield_and_department()
    {
        var response = await PostLoginAsync("fire.chief", MasterSeed.DemoPassword);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var cookie = Cookie(response, "GovErp.Auth") ?? throw new InvalidOperationException("Auth cookie was not issued.");
        var ticket = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme)
            .TicketDataFormat.Unprotect(Uri.UnescapeDataString(cookie));
        ticket!.Principal.FindFirst(ActorClaims.Tenant)!.Value.Should().Be("springfield");
        ticket.Principal.FindFirst(ActorClaims.Dept)!.Value.Should().Be("6000");
    }

    [Fact]
    public void Actor_claims_round_trip()
    {
        var actor = ActorContext.Create("springfield", Guid.NewGuid(), "fire.chief",
            new HashSet<string> { Roles.DepartmentHead }, "6000");
        ActorClaims.ToActor(ActorClaims.ToPrincipal(actor)).Should().BeEquivalentTo(actor);
    }

    private async Task<HttpResponseMessage> PostLoginAsync(string user, string password)
    {
        var login = await _client.GetAsync("/login");
        login.EnsureSuccessStatusCode();
        var html = await login.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        token.Should().NotBeNullOrEmpty();
        return await _client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = user,
            ["password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static string? Cookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var line = values.FirstOrDefault(v => v.StartsWith(name + "=", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        var pair = line.Split(';', 2)[0];
        return pair[(name.Length + 1)..];
    }

    private sealed class WebFactory(SqlServerFixture fixture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Demo");
            builder.ConfigureAppConfiguration((_, config) => config.AddConfiguration(fixture.Configuration));
        }
    }
}

[CollectionDefinition("sql")]
public sealed class SqlCollection : ICollectionFixture<SqlServerFixture>;
