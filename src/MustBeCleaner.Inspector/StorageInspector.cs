using MustBeCleaner.Domain;
using MustBeCleaner.Infrastructure.Interop;

namespace MustBeCleaner.Inspector;

/// <summary>
/// Read-only metadata enumeration as a standard user (R1.1, R1.3, R1.4). Never traverses junctions,
/// symlinks, mount points, cloud or unclassified reparse points (R7.1, R7.2). Records access denied
/// and moves on without escalation (R6.8). Has no mutation API and no reference to the Executor.
/// </summary>
public sealed class StorageInspector
{
    private readonly IProtectionHook _protection;

    /// <summary>Creates an inspector. <paramref name="protection"/> defaults to <see cref="PolicyProtectionHook"/>.</summary>
    public StorageInspector(IProtectionHook? protection = null)
        => _protection = protection ?? new PolicyProtectionHook();

    /// <summary>Enumerates every root, returning entries plus per-root coverage (R2.5).</summary>
    public ScanResult Scan(IReadOnlyList<string> roots, CancellationToken cancellation = default)
    {
        var entries = new List<InspectedEntry>();
        var coverage = new List<RootCoverage>();
        var denied = new List<AccessDeniedRecord>();
        foreach (var root in roots)
            coverage.Add(ScanRoot(root, entries, denied, cancellation));
        return new ScanResult(entries, coverage, denied);
    }

    private RootCoverage ScanRoot(string root, List<InspectedEntry> entries, List<AccessDeniedRecord> denied, CancellationToken ct)
    {
        if (!IsAcceptableRoot(root)) return new RootCoverage(root, CoverageStatus.Rejected, 0, 0, 0);
        if (ct.IsCancellationRequested) return new RootCoverage(root, CoverageStatus.Cancelled, 0, 0, 0);

        var start = entries.Count;
        var head = Win32ReadOnly.ReadObjectId(root);
        if (head.Id is null)
        {
            if (head.ErrorCode is Win32ReadOnly.ErrorFileNotFound or Win32ReadOnly.ErrorPathNotFound)
                return new RootCoverage(root, CoverageStatus.NotFound, 0, 0, 0);
            if (head.ErrorCode == Win32ReadOnly.ErrorAccessDenied) denied.Add(new AccessDeniedRecord(root));
            return new RootCoverage(root, CoverageStatus.Partial, 0, 0, head.ErrorCode == Win32ReadOnly.ErrorAccessDenied ? 1 : 0);
        }

        var rootId = head.Id.Value;
        var skipped = 0;
        var deniedCount = 0;
        var gaps = false;
        var cancelled = false;
        var stack = new Stack<string>();

        var rootIsReparse = (rootId.Attributes & Win32ReadOnly.AttributeReparsePoint) != 0;
        var rootIsDir = (rootId.Attributes & Win32ReadOnly.AttributeDirectory) != 0;
        entries.Add(Describe(root, rootId.Attributes, 0, rootId, rootIsReparse)); // tag unknown for a root: Unclassified
        if (rootIsReparse) skipped++;
        else if (rootIsDir) stack.Push(root);

        while (stack.Count > 0)
        {
            if (ct.IsCancellationRequested) { cancelled = true; break; }
            var dir = stack.Pop();
            var listing = Win32ReadOnly.ListDirectory(dir);
            if (!listing.Succeeded)
            {
                gaps = true;
                if (listing.ErrorCode == Win32ReadOnly.ErrorAccessDenied)
                {
                    denied.Add(new AccessDeniedRecord(dir));
                    deniedCount++;
                }
            }

            foreach (var raw in listing.Entries)
            {
                var full = Path.Combine(dir, raw.Name);
                var isReparse = (raw.Attributes & Win32ReadOnly.AttributeReparsePoint) != 0;
                var id = Win32ReadOnly.ReadObjectId(full);
                entries.Add(Describe(full, raw.Attributes, raw.ReparseTag, id.Id, isReparse));
                if (isReparse) skipped++;
                else if ((raw.Attributes & Win32ReadOnly.AttributeDirectory) != 0) stack.Push(full);
            }
        }

        var status = cancelled ? CoverageStatus.Cancelled : gaps ? CoverageStatus.Partial : CoverageStatus.Complete;
        return new RootCoverage(root, status, entries.Count - start, skipped, deniedCount);
    }

    private InspectedEntry Describe(string path, uint attributes, uint reparseTag, RawObjectId? id, bool isReparse)
    {
        var isDirectory = (attributes & Win32ReadOnly.AttributeDirectory) != 0;
        var kind = isReparse ? EntryKind.ReparsePoint : isDirectory ? EntryKind.Directory : EntryKind.File;
        var reparse = isReparse ? ReparseClassifier.Classify(reparseTag) : ReparseKind.None;
        var verdict = _protection.Evaluate(path, isDirectory);

        ItemIdentity? identity = null;
        if (id is { } raw)
        {
            identity = new ItemIdentity(
                raw.VolumeSerial.ToString("X16"),
                raw.FileIdHigh.ToString("X16") + raw.FileIdLow.ToString("X16"),
                PathPolicy.Normalize(path) is { IsCanonical: true } n ? n.Path!.Value.Value : path.ToUpperInvariant(), // R7.6; 8.3 names fall back
                isDirectory ? null : raw.Size,
                ToUtc(raw.LastWriteFileTime),
                (FileAttributes)raw.Attributes);
        }
        return new InspectedEntry(path, kind, reparse, identity, id?.LinkCount, verdict.IsProtected, verdict.RuleId);
    }

    private static DateTime ToUtc(long fileTime)
        => fileTime is > 0 and < 2650467743999999999 ? DateTime.FromFileTimeUtc(fileTime) : DateTime.MinValue;

    private static bool IsAcceptableRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || root.Length < 3) return false;
        if (!char.IsAsciiLetter(root[0]) || root[1] != ':' || root[2] != '\\') return false;
        if (root.IndexOf(':', 2) >= 0) return false;
        return !root.Split('\\').Any(s => s is "." or "..");
    }
}
