using MustBeCleaner.Adapters;
using Xunit;

namespace MustBeCleaner.Adapters.Tests;

/// <summary>Slice 1: a well-formed declarative Adapter loads with every R8.1 field.</summary>
public class AdapterLoadsTests
{
    [Fact]
    public void Valid_adapter_exposes_every_R8_1_field()
    {
        var result = AdapterLoader.Parse(AdapterJson.Valid());

        Assert.True(result.IsValid, string.Join("; ", result.Violations));
        var a = result.Adapter!;
        Assert.Equal("codex.tmp", a.RuleId);
        Assert.Equal("1.0.0", a.Version);
        Assert.Equal("docs/LOCAL_STORAGE_REVIEW.md", a.SourceReference);
        Assert.Equal("Codex", a.Fingerprint.AppId);
        Assert.Equal(["1.2.3"], a.Fingerprint.Versions);
        Assert.Equal([@".codex\.tmp"], a.AllowedRelativePaths);
        Assert.Equal(AdapterCategory.Temp, a.Category);
        Assert.Equal(AdapterTrust.InventoryOnly, a.Trust);
        Assert.Equal(["owner-process-active"], a.Blockers);
        Assert.Equal(["Codex.exe"], a.Processes.Owners);
        Assert.Equal(["codex-helper.exe"], a.Processes.Helpers);
        Assert.Equal(["age-over-grace-period"], a.RequiredEvidence);
        Assert.Equal(RegenerationCost.Low, a.RegenerationCost);
        Assert.Equal(RecoveryPolicy.Quarantine, a.RecoveryPolicy);
    }
}

internal static class AdapterJson
{
    /// <summary>Valid document; <paramref name="mutate"/> replaces raw JSON text for rejection tests.</summary>
    public static string Valid(Func<string, string>? mutate = null)
    {
        const string json = """
        {
          "schemaVersion": 1,
          "ruleId": "codex.tmp",
          "version": "1.0.0",
          "sourceReference": "docs/LOCAL_STORAGE_REVIEW.md",
          "fingerprint": { "appId": "Codex", "versions": ["1.2.3"] },
          "allowedRelativePaths": [".codex\\.tmp"],
          "category": "temp",
          "trust": "inventory-only",
          "blockers": ["owner-process-active"],
          "processes": { "owners": ["Codex.exe"], "helpers": ["codex-helper.exe"] },
          "requiredEvidence": ["age-over-grace-period"],
          "regenerationCost": "low",
          "recoveryPolicy": "quarantine"
        }
        """;
        return mutate is null ? json : mutate(json);
    }
}

/// <summary>The shipped policy files must stay consistent with the loader.</summary>
public class PolicyFilesTests
{
    private static string PoliciesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MustBeCleaner.sln"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("MustBeCleaner.sln not found."), "policies");
    }

    [Fact]
    public void Shipped_example_loads()
    {
        var r = AdapterLoader.LoadFile(Path.Combine(PoliciesDir(), "adapter.example.json"));
        Assert.True(r.IsValid, string.Join("; ", r.Violations));
        Assert.Equal(AdapterTrust.InventoryOnly, r.Adapter!.Trust);
    }

    [Fact]
    public void Schema_is_closed_and_requires_every_loader_field()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(PoliciesDir(), "adapter.schema.json")));
        var root = doc.RootElement;
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        var required = root.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToHashSet();
        Assert.Equal(root.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet(), required!);
    }
}
