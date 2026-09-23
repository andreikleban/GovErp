using System.Xml.Linq;

namespace GovErp.Architecture.Tests;

public sealed class LayerReferenceTests
{
    internal static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GovErp.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Solution root not found.");
        }
    }

    [Fact]
    public void Every_source_project_has_only_explicitly_allowed_dependencies()
    {
        string[] domains = ["Shared", "ChartOfAccounts", "Ledger", "Payables", "Validation"];
        var domainNames = domains.Select(x => $"GovErp.Domain.{x}").ToArray();
        foreach (var projectPath in Directory.GetFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(projectPath);
            string[] allowed = name switch
            {
                "GovErp.Domain.Shared" => [],
                _ when name.StartsWith("GovErp.Domain.", StringComparison.Ordinal) => ["GovErp.Domain.Shared"],
                "GovErp.Application.Web" => domainNames,
                "GovErp.Infrastructure" => [.. domainNames, "GovErp.Application.Web"],
                "GovErp.Web" => ["GovErp.Application.Web", "GovErp.Infrastructure"],
                "GovErp.AppHost" => ["GovErp.Web"],
                _ => throw new InvalidOperationException($"Unspecified source project: {name}")
            };
            var document = XDocument.Load(projectPath);
            var references = document.Descendants("ProjectReference")
                .Select(x => Path.GetFileNameWithoutExtension(x.Attribute("Include")!.Value)).ToArray();
            Assert.Equal(allowed.Order(), references.Order());
            if (name.StartsWith("GovErp.Domain.", StringComparison.Ordinal))
            {
                Assert.Empty(document.Descendants("PackageReference"));
                Assert.Empty(document.Descendants("FrameworkReference"));
            }
        }
    }

    [Fact]
    public void Transitive_project_references_are_disabled_and_package_versions_are_central()
    {
        var props = XDocument.Load(Path.Combine(Root, "Directory.Build.props"));
        Assert.Equal("true", props.Descendants("DisableTransitiveProjectReferences").Single().Value);
        Assert.Equal("true", props.Descendants("ManagePackageVersionsCentrally").Single().Value);
        foreach (var root in new[] { "src", "tests" })
        {
            foreach (var path in Directory.GetFiles(Path.Combine(Root, root), "*.csproj", SearchOption.AllDirectories))
            {
                Assert.All(XDocument.Load(path).Descendants("PackageReference"), p => Assert.Null(p.Attribute("Version")));
            }
        }
    }
}
