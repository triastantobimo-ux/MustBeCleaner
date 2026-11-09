namespace MustBeCleaner.TestKit.Fakes;

/// <summary>Raised by <see cref="FakeFileSystem.Checkpoint"/> to simulate a process crash at a named step.</summary>
public sealed class SimulatedCrashException(string step) : Exception($"Simulated crash at step '{step}'.")
{
    public string Step { get; } = step;
}

/// <summary>A failure to inject into <see cref="FakeFileSystem"/>. Build with the static factories.</summary>
public abstract record Fault
{
    public static Fault CrashAt(string step) => new CrashFault(step);
    public static Fault DiskFull(string volume, long capacityBytes) => new DiskFullFault(volume, capacityBytes);
    public static Fault Locked(string path) => new LockedFault(path);
    public static Fault PermissionDenied(string path) => new PermissionFault(path);
    public static Fault SourceModifiedDuringCopy(string path, byte[] newContent) => new ModifiedFault(path, newContent);
    public static Fault DestinationMissing(string volume) => new MissingFault(volume);

    internal sealed record CrashFault(string Step) : Fault;
    internal sealed record DiskFullFault(string Volume, long Capacity) : Fault;
    internal sealed record LockedFault(string Path) : Fault;
    internal sealed record PermissionFault(string Path) : Fault;
    internal sealed record ModifiedFault(string Path, byte[] NewContent) : Fault;
    internal sealed record MissingFault(string Volume) : Fault;
}

/// <summary>
/// In-memory filesystem for property tests and crash/recovery tests, with failure injection.
/// Paths are case-insensitive, accept '/' or '\', and start with a volume ("C:\...").
/// Copy never overwrites. Real disks are never touched.
/// </summary>
public sealed class FakeFileSystem
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Fault> _faults = [];

    public void Inject(Fault fault) => _faults.Add(fault);

    /// <summary>Marks a named step; throws <see cref="SimulatedCrashException"/> if a crash is injected there. State written so far is kept.</summary>
    public void Checkpoint(string step)
    {
        if (_faults.OfType<Fault.CrashFault>().Any(f => f.Step == step))
            throw new SimulatedCrashException(step);
    }

    public void CreateDirectory(string path)
    {
        path = Normalize(path);
        ThrowIfVolumeMissing(path);
        ThrowIfDenied(path);
        for (var p = path; p.Length > 0 && !_directories.Contains(p); p = Parent(p)) _directories.Add(p);
    }

    public void WriteFile(string path, byte[] content)
    {
        path = Normalize(path);
        RequireDirectory(Parent(path));
        ThrowIfDenied(path);
        ThrowIfLocked(path);
        ThrowIfNoSpace(path, content.LongLength - (_files.TryGetValue(path, out var old) ? old.LongLength : 0));
        _files[path] = (byte[])content.Clone();
    }

    public byte[] ReadFile(string path)
    {
        path = Normalize(path);
        RequireFile(path);
        ThrowIfDenied(path);
        ThrowIfLocked(path);
        return (byte[])_files[path].Clone();
    }

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path)) && !VolumeMissing(Normalize(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path)) && !VolumeMissing(Normalize(path));

    /// <summary>Copies without overwriting. If a SourceModifiedDuringCopy fault matches, the old bytes are copied and the source then changes.</summary>
    public void Copy(string source, string destination)
    {
        source = Normalize(source);
        destination = Normalize(destination);
        RequireFile(source);
        ThrowIfDenied(source);
        ThrowIfLocked(source);
        RequireDirectory(Parent(destination));
        ThrowIfDenied(destination);
        if (_files.ContainsKey(destination)) throw new IOException($"Destination exists: {destination}");

        var content = (byte[])_files[source].Clone();
        ThrowIfNoSpace(destination, content.LongLength);
        _files[destination] = content;

        var modified = _faults.OfType<Fault.ModifiedFault>().FirstOrDefault(f => Normalize(f.Path) == source);
        if (modified is not null) _files[source] = (byte[])modified.NewContent.Clone();
    }

    public void Move(string source, string destination)
    {
        Copy(source, destination);
        Delete(source);
    }

    public void Delete(string path)
    {
        path = Normalize(path);
        RequireFile(path);
        ThrowIfDenied(path);
        ThrowIfLocked(path);
        _files.Remove(path);
    }

    /// <summary>All existing files (normalized path to content copy), for asserting "at least one intact copy remains".</summary>
    public IReadOnlyDictionary<string, byte[]> Snapshot() =>
        _files.Where(kv => !VolumeMissing(kv.Key))
              .ToDictionary(kv => kv.Key, kv => (byte[])kv.Value.Clone(), StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string path)
    {
        var p = path.Replace('/', '\\').TrimEnd('\\');
        if (p.Length == 2 && p[1] == ':') p += "\\";
        return p;
    }

    private static string Parent(string path)
    {
        var i = path.LastIndexOf('\\');
        return i <= 2 ? path[..Math.Min(3, path.Length)] : path[..i];
    }

    private static string Volume(string path) => path.Length >= 2 ? path[..2].ToUpperInvariant() : path;

    private bool VolumeMissing(string path) =>
        _faults.OfType<Fault.MissingFault>().Any(f => string.Equals(f.Volume.TrimEnd('\\'), Volume(path), StringComparison.OrdinalIgnoreCase));

    private void ThrowIfVolumeMissing(string path)
    {
        if (VolumeMissing(path)) throw new DirectoryNotFoundException($"Volume not found: {Volume(path)}");
    }

    private void RequireDirectory(string dir)
    {
        ThrowIfVolumeMissing(dir);
        if (!_directories.Contains(dir) && !IsVolumeRoot(dir)) throw new DirectoryNotFoundException($"Directory not found: {dir}");
    }

    private bool IsVolumeRoot(string dir) => dir.Length <= 3 && _directories.Contains(dir);

    private void RequireFile(string path)
    {
        ThrowIfVolumeMissing(path);
        if (!_files.ContainsKey(path)) throw new FileNotFoundException($"File not found: {path}");
    }

    private void ThrowIfLocked(string path)
    {
        if (_faults.OfType<Fault.LockedFault>().Any(f => Normalize(f.Path).Equals(path, StringComparison.OrdinalIgnoreCase)))
            throw new IOException($"The process cannot access the file '{path}' because it is being used by another process.");
    }

    private void ThrowIfDenied(string path)
    {
        foreach (var f in _faults.OfType<Fault.PermissionFault>())
        {
            var root = Normalize(f.Path);
            if (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Access to '{path}' is denied.");
        }
    }

    private void ThrowIfNoSpace(string path, long additionalBytes)
    {
        foreach (var f in _faults.OfType<Fault.DiskFullFault>())
        {
            if (!string.Equals(f.Volume.TrimEnd('\\'), Volume(path), StringComparison.OrdinalIgnoreCase)) continue;
            var used = _files.Where(kv => Volume(kv.Key) == Volume(path)).Sum(kv => kv.Value.LongLength);
            if (used + additionalBytes > f.Capacity)
                throw new IOException("There is not enough space on the disk.");
        }
    }
}
