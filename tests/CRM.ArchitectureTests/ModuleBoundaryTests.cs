using System.Reflection;
using CRM.BuildingBlocks.Modules;
using NetArchTest.Rules;

namespace CRM.ArchitectureTests;

/// <summary>
/// Guards the modular-monolith rules. Modules are discovered from the host's references,
/// so a newly added module is covered automatically.
/// </summary>
public class ModuleBoundaryTests
{
    private const string ModulePrefix = "CRM.Modules.";
    private const string HostNamespace = "CRM.API";
    private const string BuildingBlocksNamespace = "CRM.BuildingBlocks";

    public static IReadOnlyList<Assembly> ModuleAssemblies { get; } = typeof(Program).Assembly
        .GetReferencedAssemblies()
        .Where(a => a.Name!.StartsWith(ModulePrefix, StringComparison.Ordinal))
        .Select(Assembly.Load)
        .ToList();

    public static TheoryData<string> ModuleNames => new(ModuleAssemblies.Select(a => a.GetName().Name!));

    private static Assembly Module(string name) => ModuleAssemblies.Single(a => a.GetName().Name == name);

    [Fact]
    public void Host_ReferencesAtLeastTwoModules()
    {
        Assert.True(ModuleAssemblies.Count >= 2, "Module discovery found too few modules; the test would prove nothing.");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_OnlyUsesOtherModulesThroughTheirContracts(string moduleName)
    {
        var module = Module(moduleName);

        foreach(var other in ModuleAssemblies.Where(a => a != module))
        {
            var forbidden = InternalNamespacesAndRootTypes(other);

            var result = Types.InAssembly(module).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

            Assert.True(result.IsSuccessful,
                $"{moduleName} depends on internals of {other.GetName().Name}: {string.Join(", ", result.FailingTypeNames ?? [])}. " +
                "Use the other module's Contracts (integration events, public queries) instead.");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_DoesNotDependOnHost(string moduleName)
    {
        var result = Types.InAssembly(Module(moduleName)).ShouldNot().HaveDependencyOn(HostNamespace).GetResult();

        Assert.True(result.IsSuccessful, $"{moduleName} depends on the host: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_ExposesOnlyItsModuleClassContractsAndMigrations(string moduleName)
    {
        var module = Module(moduleName);

        var leaked = module.GetExportedTypes()
            .Where(t => !typeof(IModule).IsAssignableFrom(t))
            .Where(t => t.Namespace?.EndsWith(".Contracts", StringComparison.Ordinal) != true)
            .Where(t => t.Namespace?.EndsWith(".Migrations", StringComparison.Ordinal) != true)
            .Select(t => t.FullName)
            .ToList();

        Assert.True(leaked.Count == 0, $"{moduleName} exposes public types outside Contracts: {string.Join(", ", leaked)}. Make them internal.");
    }

    [Fact]
    public void BuildingBlocks_DoesNotDependOnModulesOrHost()
    {
        var result = Types.InAssembly(typeof(IModule).Assembly)
            .ShouldNot().HaveDependencyOnAny("CRM.Modules", HostNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, $"BuildingBlocks depends on: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_HasExactlyOneModuleClass(string moduleName)
    {
        var modules = Module(moduleName).GetTypes().Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract).ToList();

        Assert.Single(modules);
        Assert.StartsWith(BuildingBlocksNamespace, typeof(IModule).Namespace);
    }

    /// <summary>Every namespace of <paramref name="module"/> except <c>*.Contracts</c>, plus its root-namespace types.</summary>
    private static string[] InternalNamespacesAndRootTypes(Assembly module)
    {
        var root = module.GetName().Name!;
        var types = module.GetTypes().Where(t => t.Namespace is not null).ToList();

        var namespaces = types
            .Select(t => t.Namespace!)
            .Where(ns => ns != root && !ns.EndsWith(".Contracts", StringComparison.Ordinal))
            .Distinct();

        // Types directly in the root namespace (e.g. the module class) can't be matched by a sub-namespace.
        var rootTypes = types.Where(t => t.Namespace == root).Select(t => t.FullName!);

        return namespaces.Concat(rootTypes).ToArray();
    }
}
