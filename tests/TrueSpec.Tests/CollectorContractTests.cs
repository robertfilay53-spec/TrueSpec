using System.Reflection;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Collector plug-ins, phase 1 (plan Beyond v2.0 item 2): the collector
/// contract is versioned, and its shape is pinned so a change to
/// ICollector cannot drift silently — a new member, a renamed property,
/// or a changed signature breaks this test and forces a deliberate
/// contract-version bump. Runs on both legs.
/// </summary>
public class CollectorContractTests
{
    [Fact]
    public void CurrentVersion_IsOne()
    {
        Assert.Equal(1, CollectorContract.CurrentVersion);
    }

    [Fact]
    public void ICollector_ShapeIsPinned()
    {
        var type = typeof(ICollector);

        AssertProperty(type, "Id", typeof(string));
        AssertProperty(type, "Name", typeof(string));
        AssertProperty(type, "Category", typeof(string));
        AssertProperty(type, "RefreshInterval", typeof(TimeSpan));
        AssertProperty(type, "RequiresAdministrator", typeof(bool));

        var collect = type.GetMethod("CollectAsync");
        Assert.NotNull(collect);
        Assert.Equal(typeof(Task<ComponentSnapshot>), collect.ReturnType);
        var parameters = collect.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(CancellationToken), parameters[0].ParameterType);
        Assert.True(parameters[0].HasDefaultValue);
    }

    private static void AssertProperty(Type type, string name, Type propertyType)
    {
        var property = type.GetProperty(name);
        Assert.NotNull(property);
        Assert.Equal(propertyType, property.PropertyType);
        Assert.NotNull(property.GetMethod);
    }
}
