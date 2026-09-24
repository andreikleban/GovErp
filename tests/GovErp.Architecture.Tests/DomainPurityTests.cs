using System.Reflection;
using System.Runtime.CompilerServices;

namespace GovErp.Architecture.Tests;

public sealed class DomainPurityTests
{
    private static IEnumerable<Assembly> Domains => new[] { "Shared", "ChartOfAccounts", "Ledger", "Payables", "Validation" }
        .Select(name => Assembly.Load($"GovErp.Domain.{name}"));

    [Fact]
    public void Domain_assemblies_do_not_depend_on_external_frameworks_or_other_contexts()
    {
        foreach (var assembly in Domains)
        {
            var references = assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
            Assert.DoesNotContain(references, n => n.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                n.StartsWith("System.Data", StringComparison.Ordinal) || n.StartsWith("GovErp.Infrastructure", StringComparison.Ordinal));
            Assert.DoesNotContain(references, n => n.StartsWith("GovErp.Domain.", StringComparison.Ordinal) && n != "GovErp.Domain.Shared");
        }
    }

    [Fact]
    public void Domain_values_and_entities_have_no_public_mutable_properties_or_fields()
    {
        var types = Domains.SelectMany(a => a.GetExportedTypes()).Where(t => !t.IsEnum && !typeof(Exception).IsAssignableFrom(t) &&
            (t.Namespace?.EndsWith(".Entities", StringComparison.Ordinal) == true || t.Namespace?.EndsWith(".ValueObjects", StringComparison.Ordinal) == true));
        foreach (var type in types)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.False(property.SetMethod is { IsPublic: true } setter &&
                    !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)), $"Public setter: {type.Name}.{property.Name}");
            }
            Assert.DoesNotContain(type.GetFields(BindingFlags.Public | BindingFlags.Instance), f => !f.IsInitOnly);
        }
    }

    [Fact]
    public void Domain_services_are_stateless()
    {
        var types = Domains.SelectMany(a => a.GetTypes()).Where(t =>
            t.Namespace?.Contains(".DomainServices", StringComparison.Ordinal) == true
            && !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));
        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance), f => !f.IsInitOnly);
        }
    }

    [Fact]
    public void Ui_facing_app_services_do_not_expose_entities()
    {
        var app = typeof(GovErp.Application.Web.Common.ActorContext).Assembly;
        var entityTypes = Domains.SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.EndsWith(".Entities", StringComparison.Ordinal) == true).ToHashSet();
        var services = app.GetTypes()
            .Where(t => t.IsInterface && t.Name.StartsWith('I') && t.Name.EndsWith("AppService", StringComparison.Ordinal)).ToList();
        services.Should().NotBeEmpty();
        var offenders = services
            .SelectMany(t => t.GetMethods())
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .SelectMany(Unwrap)
            .Where(entityTypes.Contains).Select(t => t.FullName).Distinct().ToList();
        offenders.Should().BeEmpty(because: "CA-10: UI-facing services return view models");

        static IEnumerable<Type> Unwrap(Type t) => t.IsGenericType ? t.GetGenericArguments().SelectMany(Unwrap).Append(t) : [t];
    }

    [Fact]
    public void Shared_kernel_contains_only_values()
    {
        Assert.All(Assembly.Load("GovErp.Domain.Shared").GetExportedTypes(),
            t => Assert.Equal("GovErp.Domain.Shared.ValueObjects", t.Namespace));
    }
}
