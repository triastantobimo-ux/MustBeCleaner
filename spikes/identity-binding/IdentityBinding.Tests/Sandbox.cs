namespace IdentityBinding.Tests;

/// <summary>Fresh temp directory. Dispose deletes only this directory, after a guard check.</summary>
public sealed class Sandbox : IDisposable
{
    public string Root { get; }

    public Sandbox()
    {
        Root = Path.Combine(Path.GetTempPath(), "mbc-idspike-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Path_(string rel) => Path.Combine(Root, rel);

    public string WriteFile(string rel, string content)
    {
        var p = Path_(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
        return p;
    }

    public void Dispose()
    {
        var name = Path.GetFileName(Root);
        if (!name.StartsWith("mbc-idspike-") || !Root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to delete outside sandbox: " + Root);
        // Remove junctions first so recursive delete never follows them.
        foreach (var d in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories).ToList())
            if (Directory.Exists(d) && new DirectoryInfo(d).Attributes.HasFlag(FileAttributes.ReparsePoint))
                Directory.Delete(d, false);
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
    }
}
