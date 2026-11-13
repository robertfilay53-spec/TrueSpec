using System.Text;
using TrueSpec.Core.Plugins;

namespace TrueSpec.Tests;

/// <summary>
/// Collector plug-ins, phase 1 (plan Beyond v2.0 item 2): the manifest
/// parser is strict on purpose — a manifest is a file the user (or a
/// download) placed in the data folder, so every field is checked before
/// it is trusted. Runs on both legs.
/// </summary>
public class PluginManifestTests
{
    private static MemoryStream Manifest(string json) =>
        new(Encoding.UTF8.GetBytes(json));

    private static string ValidJson(Func<string, string>? mutate = null)
    {
        var json = """
            {
              "id": "com.example.gpuz",
              "name": "GPU-Z bridge",
              "version": "1.0.0",
              "contractVersion": 1,
              "assembly": "GpuZBridge.dll",
              "collectors": [
                { "id": "gpu-extra", "name": "GPU extra", "category": "GPU" }
              ]
            }
            """;
        return mutate is null ? json : mutate(json);
    }

    [Fact]
    public void ValidManifest_Parses()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson()));

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(result.Manifest);
        var manifest = result.Manifest!;
        Assert.Equal("com.example.gpuz", manifest.Id);
        Assert.Equal("GPU-Z bridge", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal(1, manifest.ContractVersion);
        Assert.Equal("GpuZBridge.dll", manifest.Assembly);
        var collector = Assert.Single(manifest.Collectors);
        Assert.Equal("gpu-extra", collector.Id);
        Assert.Equal("GPU extra", collector.Name);
        Assert.Equal("GPU", collector.Category);
    }

    [Fact]
    public void MissingId_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("\"id\": \"com.example.gpuz\",", string.Empty))));

        Assert.False(result.Success);
        Assert.Null(result.Manifest);
        Assert.Contains("id", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingName_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("\"name\": \"GPU-Z bridge\",", string.Empty))));

        Assert.False(result.Success);
        Assert.Contains("name", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingVersion_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("\"version\": \"1.0.0\",", string.Empty))));

        Assert.False(result.Success);
        Assert.Contains("version", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingContractVersion_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("\"contractVersion\": 1,", string.Empty))));

        Assert.False(result.Success);
        Assert.Contains("contractVersion", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractVersionAsString_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("\"contractVersion\": 1", "\"contractVersion\": \"1\""))));

        Assert.False(result.Success);
        Assert.Contains("contractVersion", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownTopLevelField_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("\"version\": \"1.0.0\",", "\"version\": \"1.0.0\", \"surprise\": true,"))));

        Assert.False(result.Success);
        Assert.Contains("surprise", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCollectorField_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("\"category\": \"GPU\"", "\"category\": \"GPU\", \"extra\": 1"))));

        Assert.False(result.Success);
        Assert.Contains("extra", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyWithDirectoryTraversal_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("GpuZBridge.dll", "../evil.dll"))));

        Assert.False(result.Success);
        Assert.Contains("assembly", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyWithSubfolder_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("GpuZBridge.dll", "sub/GpuZBridge.dll"))));

        Assert.False(result.Success);
        Assert.Contains("assembly", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyNotDll_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("GpuZBridge.dll", "GpuZBridge.exe"))));

        Assert.False(result.Success);
        Assert.Contains(".dll", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyCollectors_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest("""
            {
              "id": "com.example.empty",
              "name": "Empty",
              "version": "1.0.0",
              "contractVersion": 1,
              "assembly": "Empty.dll",
              "collectors": []
            }
            """));

        Assert.False(result.Success);
        Assert.Contains("no collectors", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicateCollectorIds_Fail()
    {
        var result = PluginManifestParser.Parse(Manifest("""
            {
              "id": "com.example.dupe",
              "name": "Dupe",
              "version": "1.0.0",
              "contractVersion": 1,
              "assembly": "Dupe.dll",
              "collectors": [
                { "id": "gpu-extra", "name": "One", "category": "GPU" },
                { "id": "gpu-extra", "name": "Two", "category": "GPU" }
              ]
            }
            """));

        Assert.False(result.Success);
        Assert.Contains("gpu-extra", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void BadCollectorId_Fails()
    {
        var result = PluginManifestParser.Parse(Manifest(ValidJson(j => j.Replace("\"gpu-extra\"", "\"GPU_Extra\""))));

        Assert.False(result.Success);
        Assert.Contains("GPU_Extra", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void NotJson_FailsWithoutThrowing()
    {
        var result = PluginManifestParser.Parse(Manifest("this is not json {{{"));

        Assert.False(result.Success);
        Assert.Null(result.Manifest);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void OversizedManifest_FailsWithoutThrowing()
    {
        var padding = new string('x', PluginManifestParser.MaxManifestBytes);
        var json = "{\"id\": \"" + padding + "\"}";

        var result = PluginManifestParser.Parse(Manifest(json));

        Assert.False(result.Success);
        Assert.Contains("too large", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NullStream_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => PluginManifestParser.Parse(null!));
    }
}
