using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MustBeCleaner.Adapters;
using Xunit;

namespace MustBeCleaner.Adapters.Tests;

/// <summary>Builds signed update packages in memory with a throwaway key.</summary>
internal sealed class PackageBuilder : IDisposable
{
    public ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public IProvenanceVerifier Verifier => new EcdsaProvenanceVerifier(Key.ExportSubjectPublicKeyInfo());

    public UpdatePackage Build(params (string Name, string Json)[] files)
    {
        var pf = files.Select(f => new PackageFile(f.Name, Encoding.UTF8.GetBytes(f.Json))).ToList();
        return Sign(pf, pf.Select(f => (f.Name, Hex(f.Content))).ToList());
    }

    public UpdatePackage Sign(List<PackageFile> files, List<(string Name, string Sha)> entries)
    {
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = 1,
            files = entries.Select(e => new { name = e.Name, sha256 = e.Sha }),
        });
        return new UpdatePackage(manifest, SignBytes(manifest), files);
    }

    public byte[] SignBytes(byte[] data) =>
        Key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public static string Hex(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    public void Dispose() => Key.Dispose();
}

internal static class Catalogs
{
    public static Adapter Load(string json)
    {
        var r = AdapterLoader.Parse(json);
        Assert.True(r.IsValid, string.Join("; ", r.Violations));
        return r.Adapter!;
    }

    /// <summary>Valid base document with replaced paths / versions / trust / blockers.</summary>
    public static string Json(string rule = "codex.tmp", string paths = "[\".codex\\\\.tmp\"]",
        string versions = "[\"1.2.3\"]", string trust = "inventory-only",
        string blockers = "[\"owner-process-active\"]", string evidence = "[\"age-over-grace-period\"]") =>
        AdapterJson.Valid(j => j
            .Replace("\"codex.tmp\"", $"\"{rule}\"")
            .Replace("[\".codex\\\\.tmp\"]", paths)
            .Replace("[\"1.2.3\"]", versions)
            .Replace("\"inventory-only\"", $"\"{trust}\"")
            .Replace("[\"owner-process-active\"]", blockers)
            .Replace("[\"age-over-grace-period\"]", evidence));
}

/// <summary>R8.14 / R23.5: provenance-verified offline import, scope diff, no silent widening.</summary>
public class AdapterUpdateImportTests : IDisposable
{
    private readonly PackageBuilder _pkg = new();
    public void Dispose() => _pkg.Dispose();

    private static readonly Adapter Base = Catalogs.Load(Catalogs.Json());

    private ImportResult Import(UpdatePackage p, params Adapter[] current) =>
        AdapterUpdates.Import(p, current, _pkg.Verifier);

    // ---- provenance -------------------------------------------------------

    [Fact]
    public void Signed_package_with_matching_hashes_is_accepted()
    {
        var r = Import(_pkg.Build(("a.json", Catalogs.Json(rule: "new.rule"))));
        Assert.True(r.IsAccepted, string.Join("; ", r.Rejections));
    }

    [Fact]
    public void Signature_from_an_untrusted_key_is_rejected()
    {
        using var other = new PackageBuilder();
        var p = other.Build(("a.json", Catalogs.Json()));
        var r = Import(p);
        Assert.Contains(r.Rejections, x => x.Code == ImportRejectionCode.BadSignature);
        Assert.Null(r.Diff);
    }

    [Fact]
    public void Tampered_manifest_is_rejected()
    {
        var p = _pkg.Build(("a.json", Catalogs.Json()));
        p.Manifest[^2] ^= 0x01;
        Assert.Contains(Import(p).Rejections, x => x.Code == ImportRejectionCode.BadSignature);
    }

    [Fact]
    public void Missing_or_garbage_signature_is_rejected_without_throwing()
    {
        var good = _pkg.Build(("a.json", Catalogs.Json()));
        foreach (var sig in new[] { Array.Empty<byte>(), new byte[3], new byte[64] })
        {
            var r = Import(good with { Signature = sig });
            Assert.Contains(r.Rejections, x => x.Code == ImportRejectionCode.BadSignature);
        }
    }

    [Fact]
    public void Tampered_file_content_is_rejected_by_hash()
    {
        var p = _pkg.Build(("a.json", Catalogs.Json()));
        var evil = new PackageFile("a.json", Encoding.UTF8.GetBytes(Catalogs.Json(paths: "[\".codex\"]")));
        var r = Import(p with { Files = [evil] });
        Assert.Contains(r.Rejections, x => x.Code == ImportRejectionCode.HashMismatch);
    }

    [Fact]
    public void File_not_listed_in_the_signed_manifest_is_rejected()
    {
        var p = _pkg.Build(("a.json", Catalogs.Json()));
        var extra = new PackageFile("b.json", Encoding.UTF8.GetBytes(Catalogs.Json(rule: "x.y")));
        var r = Import(p with { Files = [.. p.Files, extra] });
        Assert.Contains(r.Rejections, x => x.Code == ImportRejectionCode.UnlistedFile);
    }

    [Fact]
    public void File_listed_but_absent_is_rejected()
    {
        var p = _pkg.Build(("a.json", Catalogs.Json()));
        var r = Import(p with { Files = [] });
        Assert.Contains(r.Rejections, x => x.Code == ImportRejectionCode.MissingFile);
    }

    [Theory]
    [InlineData(@"..\a.json")]
    [InlineData("sub/a.json")]
    [InlineData(@"C:\a.json")]
    public void Path_like_file_names_are_rejected(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(Catalogs.Json());
        var p = _pkg.Sign([new PackageFile(name, bytes)], [(name, PackageBuilder.Hex(bytes))]);
        Assert.Contains(Import(p).Rejections, x => x.Code == ImportRejectionCode.InvalidFileName);
    }

    [Fact]
    public void Signed_but_malformed_manifest_is_rejected()
    {
        var junk = Encoding.UTF8.GetBytes("not json");
        var p = new UpdatePackage(junk, _pkg.SignBytes(junk), []);
        Assert.Contains(Import(p).Rejections, x => x.Code == ImportRejectionCode.BadManifest);
    }

    [Fact]
    public void Signed_file_with_executable_content_is_still_rejected_by_the_loader()
    {
        var bad = Catalogs.Json().Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"script\": \"x\",");
        var r = Import(_pkg.Build(("a.json", bad)));
        Assert.Contains(r.Rejections, x => x.Code == ImportRejectionCode.InvalidAdapter);
        Assert.Null(r.Diff);
    }

    [Fact]
    public void Duplicate_rule_ids_inside_a_package_are_rejected()
    {
        var r = Import(_pkg.Build(("a.json", Catalogs.Json()), ("b.json", Catalogs.Json())));
        Assert.Contains(r.Rejections, x => x.Code == ImportRejectionCode.DuplicateRuleId);
    }

    [Fact]
    public void Verifier_is_a_seam_a_fake_can_replace_the_crypto()
    {
        var pkg = _pkg.Build(("a.json", Catalogs.Json(rule: "n.r")));
        var denyAll = new FakeVerifier(false);
        var allowAll = new FakeVerifier(true);
        Assert.False(AdapterUpdates.Import(pkg, [], denyAll).IsAccepted);
        Assert.True(AdapterUpdates.Import(pkg with { Signature = [] }, [], allowAll).IsAccepted);
    }

    private sealed class FakeVerifier(bool result) : IProvenanceVerifier
    {
        public bool Verify(ReadOnlyMemory<byte> manifest, ReadOnlyMemory<byte> signature) => result;
    }

    // ---- diff -------------------------------------------------------------

    [Fact]
    public void New_adapter_is_reported_Added_and_requires_approval()
    {
        var d = Import(_pkg.Build(("a.json", Catalogs.Json(rule: "brand.new")))).Diff!;
        var c = Assert.Single(d.Changes);
        Assert.Equal("brand.new", c.RuleId);
        Assert.Equal(AdapterChangeKind.Added, c.Kind);
        Assert.Contains(ScopeExpansion.NewAdapter, c.Expansions);
        Assert.True(d.RequiresApproval);
    }

    [Fact]
    public void Identical_adapter_produces_an_empty_diff_that_needs_no_approval()
    {
        var d = Import(_pkg.Build(("a.json", Catalogs.Json())), Base).Diff!;
        Assert.Empty(d.Changes);
        Assert.False(d.RequiresApproval);
    }

    [Fact]
    public void Added_and_removed_paths_are_listed()
    {
        var upd = Catalogs.Json(paths: "[\".codex\\\\cache\"]");
        var c = Assert.Single(Import(_pkg.Build(("a.json", upd)), Base).Diff!.Changes);
        Assert.Equal([@".codex\cache"], c.AddedPaths);
        Assert.Equal([@".codex\.tmp"], c.RemovedPaths);
        Assert.Empty(c.WidenedPaths);
        Assert.Contains(ScopeExpansion.PathsAdded, c.Expansions);
    }

    [Fact]
    public void Path_covering_an_existing_path_is_reported_widened()
    {
        var upd = Catalogs.Json(paths: "[\".codex\"]");
        var c = Assert.Single(Import(_pkg.Build(("a.json", upd)), Base).Diff!.Changes);
        Assert.Equal([".codex"], c.WidenedPaths);
        Assert.Contains(ScopeExpansion.PathsAdded, c.Expansions);
    }

    [Fact]
    public void Pure_narrowing_needs_no_approval()
    {
        var wide = Catalogs.Load(Catalogs.Json(paths: "[\".codex\\\\.tmp\", \".codex\\\\cache\"]"));
        var d = Import(_pkg.Build(("a.json", Catalogs.Json())), wide).Diff!;
        var c = Assert.Single(d.Changes);
        Assert.Equal([@".codex\cache"], c.RemovedPaths);
        Assert.Empty(c.Expansions);
        Assert.False(d.RequiresApproval);
    }

    [Fact]
    public void Path_comparison_ignores_case_and_slash_style()
    {
        var upd = Catalogs.Json(paths: "[\".CODEX/.tmp\"]");
        Assert.Empty(Import(_pkg.Build(("a.json", upd)), Base).Diff!.Changes);
    }

    [Fact]
    public void Added_version_raised_trust_and_dropped_safeguards_each_need_approval()
    {
        var cases = new (string Json, ScopeExpansion Expected)[]
        {
            (Catalogs.Json(versions: "[\"1.2.3\",\"2.0.0\"]"), ScopeExpansion.VersionsAdded),
            (Catalogs.Json(trust: "validated"), ScopeExpansion.TrustRaised),
            (Catalogs.Json(blockers: "[]"), ScopeExpansion.BlockersRemoved),
            (Catalogs.Json(evidence: "[]"), ScopeExpansion.EvidenceRemoved),
        };
        foreach (var (json, expected) in cases)
        {
            var d = Import(_pkg.Build(("a.json", json)), Base).Diff!;
            Assert.Contains(expected, Assert.Single(d.Changes).Expansions);
            Assert.True(d.RequiresApproval);
        }
    }

    // ---- apply ------------------------------------------------------------

    [Fact]
    public void Import_alone_never_changes_the_current_catalog()
    {
        var current = new[] { Base };
        Import(_pkg.Build(("a.json", Catalogs.Json(paths: "[\".codex\"]"))), current);
        Assert.Equal([@".codex\.tmp"], current[0].AllowedRelativePaths);
    }

    [Fact]
    public void Apply_refuses_scope_expansion_without_approval()
    {
        var d = Import(_pkg.Build(("a.json", Catalogs.Json(paths: "[\".codex\"]"))), Base).Diff!;
        var r = AdapterUpdates.Apply([Base], d, userApproved: false);
        Assert.False(r.Applied);
        Assert.True(r.ApprovalRequired);
        Assert.Null(r.Catalog);
    }

    [Fact]
    public void Apply_with_approval_replaces_by_rule_id_and_keeps_others()
    {
        var other = Catalogs.Load(Catalogs.Json(rule: "other.rule"));
        var d = Import(_pkg.Build(("a.json", Catalogs.Json(paths: "[\".codex\"]"))), Base, other).Diff!;
        var r = AdapterUpdates.Apply([Base, other], d, userApproved: true);
        Assert.True(r.Applied);
        Assert.Equal(2, r.Catalog!.Count);
        Assert.Equal([".codex"], r.Catalog.Single(a => a.RuleId == "codex.tmp").AllowedRelativePaths);
        Assert.Same(other, r.Catalog.Single(a => a.RuleId == "other.rule"));
    }

    [Fact]
    public void Apply_without_approval_is_allowed_when_nothing_expands()
    {
        var wide = Catalogs.Load(Catalogs.Json(paths: "[\".codex\\\\.tmp\", \".codex\\\\cache\"]"));
        var d = Import(_pkg.Build(("a.json", Catalogs.Json())), wide).Diff!;
        var r = AdapterUpdates.Apply([wide], d, userApproved: false);
        Assert.True(r.Applied);
        Assert.Equal([@".codex\.tmp"], r.Catalog!.Single().AllowedRelativePaths);
    }

    [Fact]
    public void Apply_rechecks_against_the_catalog_it_is_given_so_a_stale_diff_cannot_widen_silently()
    {
        var narrowBase = Catalogs.Load(Catalogs.Json(paths: "[\".codex\\\\.tmp\", \".codex\\\\cache\"]"));
        var d = Import(_pkg.Build(("a.json", Catalogs.Json())), narrowBase).Diff!;
        Assert.False(d.RequiresApproval);
        var r = AdapterUpdates.Apply([], d, userApproved: false); // applied to a different catalog
        Assert.False(r.Applied);
        Assert.True(r.ApprovalRequired);
    }

    // ---- verifier + package + offline ------------------------------------

    [Fact]
    public void Ecdsa_verifier_accepts_only_the_trusted_key_and_exact_bytes()
    {
        var data = Encoding.UTF8.GetBytes("manifest");
        var sig = _pkg.SignBytes(data);
        var v = _pkg.Verifier;
        Assert.True(v.Verify(data, sig));
        Assert.False(v.Verify(Encoding.UTF8.GetBytes("manifesT"), sig));
        using var other = new PackageBuilder();
        Assert.False(other.Verifier.Verify(data, sig));
    }

    [Fact]
    public void Ecdsa_verifier_returns_false_for_a_malformed_trusted_key()
    {
        var v = new EcdsaProvenanceVerifier([1, 2, 3]);
        Assert.False(v.Verify(new byte[] { 1 }, new byte[64]));
    }

    [Fact]
    public void Package_loads_from_a_local_folder_and_imports()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MustBeCleaner-pkgtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var built = _pkg.Build(("a.json", Catalogs.Json(rule: "disk.rule")));
            File.WriteAllBytes(Path.Combine(dir, UpdatePackage.ManifestFileName), built.Manifest);
            File.WriteAllBytes(Path.Combine(dir, UpdatePackage.SignatureFileName), built.Signature);
            File.WriteAllBytes(Path.Combine(dir, "a.json"), built.Files[0].Content);
            var r = Import(UpdatePackage.LoadDirectory(dir));
            Assert.True(r.IsAccepted, string.Join("; ", r.Rejections));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Adapters_assembly_references_no_network_assemblies()
    {
        var refs = typeof(AdapterUpdates).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(refs, n => n.StartsWith("System.Net", StringComparison.Ordinal));
    }
}
