namespace MustBeCleaner.Adapters;

/// <summary>
/// Checks that an update package manifest was produced by a trusted publisher (R8.14, R23.5).
/// Implementations must be offline. The trusted key is local configuration, never fetched.
/// </summary>
public interface IProvenanceVerifier
{
    /// <summary>True only when <paramref name="signature"/> is a valid detached signature over <paramref name="manifest"/>.</summary>
    bool Verify(ReadOnlyMemory<byte> manifest, ReadOnlyMemory<byte> signature);
}

/// <summary>One file in an update package (an Adapter JSON document).</summary>
public sealed record PackageFile(string Name, byte[] Content);

/// <summary>
/// An offline Adapter update: <c>manifest.json</c> (file names + SHA-256), a detached signature over the
/// manifest bytes, and the files. See ADR 0032 for the scheme.
/// </summary>
public sealed record UpdatePackage(byte[] Manifest, byte[] Signature, IReadOnlyList<PackageFile> Files)
{
    public const string ManifestFileName = "manifest.json";
    public const string SignatureFileName = "manifest.sig";

    /// <summary>Reads a package from a local folder. Local files only; no network.</summary>
    public static UpdatePackage LoadDirectory(string directory)
    {
        var manifest = File.ReadAllBytes(Path.Combine(directory, ManifestFileName));
        var signature = File.ReadAllBytes(Path.Combine(directory, SignatureFileName));
        var files = Directory.EnumerateFiles(directory)
            .Where(f => !IsPackageMeta(Path.GetFileName(f)))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new PackageFile(Path.GetFileName(f), File.ReadAllBytes(f)))
            .ToList();
        return new UpdatePackage(manifest, signature, files);
    }

    private static bool IsPackageMeta(string name) =>
        name.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(SignatureFileName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Why a package was refused.</summary>
public enum ImportRejectionCode
{
    /// <summary>Signature missing or not valid for the trusted key (R8.14, R23.5).</summary>
    BadSignature,
    BadManifest,
    HashMismatch,
    MissingFile,
    UnlistedFile,
    InvalidFileName,
    /// <summary>A file failed <see cref="AdapterLoader"/> (schema or executable content, R8.2).</summary>
    InvalidAdapter,
    DuplicateRuleId,
}

public sealed record ImportRejection(ImportRejectionCode Code, string Subject, string Message)
{
    public override string ToString() => $"{Code} {Subject}: {Message}";
}

public enum AdapterChangeKind { Added, Modified }

/// <summary>Ways an update can enlarge what the app may touch or loosen protection. Each needs approval.</summary>
public enum ScopeExpansion { NewAdapter, PathsAdded, VersionsAdded, TrustRaised, BlockersRemoved, EvidenceRemoved }

/// <summary>Scope change for one Adapter. <see cref="WidenedPaths"/> are added paths that cover an existing path.</summary>
public sealed record AdapterChange(
    string RuleId,
    AdapterChangeKind Kind,
    IReadOnlyList<string> AddedPaths,
    IReadOnlyList<string> RemovedPaths,
    IReadOnlyList<string> WidenedPaths,
    IReadOnlyList<ScopeExpansion> Expansions);

/// <summary>
/// What importing would change. Carries the verified incoming Adapters; nothing is applied until
/// <see cref="AdapterUpdates.Apply"/> (R8.14).
/// </summary>
public sealed record ImportDiff(IReadOnlyList<AdapterChange> Changes, IReadOnlyList<Adapter> Incoming)
{
    /// <summary>True when any change expands scope. Such a diff cannot be applied without explicit user approval.</summary>
    public bool RequiresApproval => Changes.Any(c => c.Expansions.Count > 0);
}

/// <summary>Either a verified diff or the rejection reasons.</summary>
public sealed record ImportResult(ImportDiff? Diff, IReadOnlyList<ImportRejection> Rejections)
{
    public bool IsAccepted => Diff is not null && Rejections.Count == 0;
}

/// <summary>Outcome of applying a diff. <see cref="Catalog"/> is null when refused.</summary>
public sealed record ApplyResult(IReadOnlyList<Adapter>? Catalog, bool ApprovalRequired)
{
    public bool Applied => Catalog is not null;
}

/// <summary>Offline Adapter update import (R8.14, R23.5). Pure: reads nothing, writes nothing, no network.</summary>
public static class AdapterUpdates
{
    /// <summary>
    /// Verifies provenance, then diffs the package against <paramref name="current"/>. A package is a delta:
    /// Adapters in it add or replace by rule ID; others are untouched. The signature is checked before the
    /// manifest is even parsed, and every file must match its signed SHA-256 (R8.14, R23.5).
    /// </summary>
    public static ImportResult Import(UpdatePackage package, IReadOnlyList<Adapter> current, IProvenanceVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(verifier);

        if (!verifier.Verify(package.Manifest, package.Signature))
            return Reject(new ImportRejection(ImportRejectionCode.BadSignature, UpdatePackage.ManifestFileName,
                "Signature is missing or not valid for the trusted key."));

        var rejections = new List<ImportRejection>();
        var listed = ReadManifest(package.Manifest, rejections);
        if (listed is null) return new ImportResult(null, rejections);

        var byName = package.Files.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var verified = new List<PackageFile>();
        foreach (var (name, sha) in listed)
        {
            if (!IsPlainFileName(name))
            {
                rejections.Add(new(ImportRejectionCode.InvalidFileName, name, "File name must be a plain name without folders."));
                continue;
            }
            if (!byName.TryGetValue(name, out var file))
            {
                rejections.Add(new(ImportRejectionCode.MissingFile, name, "Listed in the signed manifest but not in the package."));
                continue;
            }
            var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file.Content));
            if (!actual.Equals(sha, StringComparison.OrdinalIgnoreCase))
            {
                rejections.Add(new(ImportRejectionCode.HashMismatch, name, "Content does not match the signed SHA-256."));
                continue;
            }
            verified.Add(file);
        }
        foreach (var f in package.Files)
            if (!listed.Any(l => l.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase)))
                rejections.Add(new(ImportRejectionCode.UnlistedFile, f.Name, "Not listed in the signed manifest."));
        if (rejections.Count > 0) return new ImportResult(null, rejections);

        var incoming = new List<Adapter>();
        foreach (var f in verified)
        {
            var r = AdapterLoader.Parse(System.Text.Encoding.UTF8.GetString(f.Content));
            if (!r.IsValid)
            {
                foreach (var v in r.Violations)
                    rejections.Add(new(ImportRejectionCode.InvalidAdapter, f.Name, v.ToString()));
                continue;
            }
            if (incoming.Any(a => a.RuleId == r.Adapter!.RuleId))
                rejections.Add(new(ImportRejectionCode.DuplicateRuleId, f.Name, $"Rule ID '{r.Adapter!.RuleId}' appears twice."));
            else
                incoming.Add(r.Adapter!);
        }
        if (rejections.Count > 0) return new ImportResult(null, rejections);

        return new ImportResult(Diff(current, incoming), []);
    }

    /// <summary>
    /// Applies a diff to <paramref name="current"/>. The diff is recomputed against <paramref name="current"/>;
    /// if it expands scope and <paramref name="userApproved"/> is false, nothing is applied (R8.14).
    /// </summary>
    public static ApplyResult Apply(IReadOnlyList<Adapter> current, ImportDiff diff, bool userApproved)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(diff);
        var actual = Diff(current, diff.Incoming);
        if (actual.RequiresApproval && !userApproved) return new ApplyResult(null, true);

        var result = current.ToList();
        foreach (var a in diff.Incoming)
        {
            var i = result.FindIndex(x => x.RuleId == a.RuleId);
            if (i >= 0) result[i] = a; else result.Add(a);
        }
        return new ApplyResult(result, actual.RequiresApproval);
    }

    private static ImportResult Reject(ImportRejection r) => new(null, [r]);

    private static bool IsPlainFileName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name is not ("." or "..")
        && name.IndexOfAny(['\\', '/', ':']) < 0;

    private static List<(string Name, string Sha)>? ReadManifest(byte[] manifest, List<ImportRejection> rejections)
    {
        const string subject = UpdatePackage.ManifestFileName;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(manifest);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object
                || !root.TryGetProperty("formatVersion", out var fv) || !fv.TryGetInt32(out var v) || v != 1
                || !root.TryGetProperty("files", out var files) || files.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                rejections.Add(new(ImportRejectionCode.BadManifest, subject, "Expected {formatVersion:1, files:[...]}."));
                return null;
            }
            var list = new List<(string, string)>();
            foreach (var e in files.EnumerateArray())
            {
                if (e.ValueKind != System.Text.Json.JsonValueKind.Object
                    || !e.TryGetProperty("name", out var n) || n.ValueKind != System.Text.Json.JsonValueKind.String
                    || !e.TryGetProperty("sha256", out var s) || s.ValueKind != System.Text.Json.JsonValueKind.String)
                {
                    rejections.Add(new(ImportRejectionCode.BadManifest, subject, "Each file needs string 'name' and 'sha256'."));
                    return null;
                }
                list.Add((n.GetString()!, s.GetString()!));
            }
            return list;
        }
        catch (System.Text.Json.JsonException e)
        {
            rejections.Add(new(ImportRejectionCode.BadManifest, subject, e.Message));
            return null;
        }
    }

    // ---- diff ---------------------------------------------------------------

    private static ImportDiff Diff(IReadOnlyList<Adapter> current, IReadOnlyList<Adapter> incoming)
    {
        var changes = new List<AdapterChange>();
        foreach (var n in incoming.OrderBy(a => a.RuleId, StringComparer.Ordinal))
        {
            var old = current.FirstOrDefault(a => a.RuleId == n.RuleId);
            if (old is null)
            {
                changes.Add(new(n.RuleId, AdapterChangeKind.Added, n.AllowedRelativePaths, [], [], [ScopeExpansion.NewAdapter]));
                continue;
            }
            if (Equivalent(old, n)) continue;

            var oldKeys = old.AllowedRelativePaths.Select(PathKey).ToHashSet();
            var newKeys = n.AllowedRelativePaths.Select(PathKey).ToHashSet();
            var added = n.AllowedRelativePaths.Where(p => !oldKeys.Contains(PathKey(p))).ToList();
            var removed = old.AllowedRelativePaths.Where(p => !newKeys.Contains(PathKey(p))).ToList();
            var widened = added.Where(p => old.AllowedRelativePaths.Any(o => StrictlyCovers(p, o))).ToList();

            var exp = new List<ScopeExpansion>();
            if (added.Count > 0) exp.Add(ScopeExpansion.PathsAdded);
            if (n.Fingerprint.Versions.Any(x => !old.Fingerprint.Versions.Contains(x, StringComparer.Ordinal)))
                exp.Add(ScopeExpansion.VersionsAdded);
            if (old.Trust == AdapterTrust.InventoryOnly && n.Trust == AdapterTrust.Validated)
                exp.Add(ScopeExpansion.TrustRaised);
            if (old.Blockers.Any(b => !n.Blockers.Contains(b, StringComparer.Ordinal)))
                exp.Add(ScopeExpansion.BlockersRemoved);
            if (old.RequiredEvidence.Any(b => !n.RequiredEvidence.Contains(b, StringComparer.Ordinal)))
                exp.Add(ScopeExpansion.EvidenceRemoved);

            changes.Add(new(n.RuleId, AdapterChangeKind.Modified, added, removed, widened, exp));
        }
        return new ImportDiff(changes, incoming);
    }

    private static string PathKey(string p) =>
        string.Join('\\', AdapterMatcher.Segments(p) ?? [p]).ToUpperInvariant();

    private static bool StrictlyCovers(string wide, string narrow)
    {
        var w = AdapterMatcher.Segments(wide);
        var n = AdapterMatcher.Segments(narrow);
        return w is not null && n is not null && w.Length < n.Length && AdapterMatcher.StartsWith(n, w);
    }

    private static bool SameSet(IReadOnlyList<string> a, IReadOnlyList<string> b, Func<string, string> key) =>
        a.Select(key).Order(StringComparer.Ordinal).SequenceEqual(b.Select(key).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static bool Equivalent(Adapter a, Adapter b) =>
        a.Version == b.Version && a.SourceReference == b.SourceReference
        && a.Fingerprint.AppId == b.Fingerprint.AppId
        && SameSet(a.Fingerprint.Versions, b.Fingerprint.Versions, s => s)
        && SameSet(a.AllowedRelativePaths, b.AllowedRelativePaths, PathKey)
        && a.Category == b.Category && a.Trust == b.Trust
        && SameSet(a.Blockers, b.Blockers, s => s)
        && SameSet(a.Processes.Owners, b.Processes.Owners, s => s)
        && SameSet(a.Processes.Helpers, b.Processes.Helpers, s => s)
        && SameSet(a.RequiredEvidence, b.RequiredEvidence, s => s)
        && a.RegenerationCost == b.RegenerationCost && a.RecoveryPolicy == b.RecoveryPolicy;
}
