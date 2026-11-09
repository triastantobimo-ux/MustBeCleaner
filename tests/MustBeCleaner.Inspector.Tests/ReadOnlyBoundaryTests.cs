using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using MustBeCleaner.Inspector;

namespace MustBeCleaner.Inspector.Tests;

/// <summary>
/// Structural guard for R1.3: the Inspector assembly names no Executor and no filesystem mutation API,
/// and the read-only Win32 interop imports only an allowlist of read calls.
/// </summary>
public class ReadOnlyBoundaryTests
{
    private static readonly Dictionary<string, HashSet<string>?> BannedMembers = new()
    {
        ["System.IO.File"] = ["Delete", "Move", "Copy", "Create", "CreateText", "AppendAllText", "AppendAllLines", "AppendAllBytes", "AppendText",
            "WriteAllText", "WriteAllBytes", "WriteAllLines", "Replace", "SetAttributes", "SetUnixFileMode", "Encrypt", "Decrypt",
            "CreateSymbolicLink", "Open", "OpenWrite", "SetCreationTime", "SetLastWriteTime", "SetLastAccessTime"],
        ["System.IO.Directory"] = ["Delete", "Move", "CreateDirectory", "CreateSymbolicLink", "SetCreationTime", "SetLastWriteTime", "SetLastAccessTime"],
        ["System.IO.FileInfo"] = ["Delete", "MoveTo", "CopyTo", "Create", "CreateText", "AppendText", "Replace", "Encrypt", "Decrypt", "OpenWrite", "Open", "CreateAsSymbolicLink"],
        ["System.IO.DirectoryInfo"] = ["Delete", "MoveTo", "Create", "CreateSubdirectory", "CreateAsSymbolicLink"],
        ["System.IO.FileStream"] = null,
        ["System.IO.StreamWriter"] = null,
        ["System.IO.FileSystemAclExtensions"] = null,
        ["System.Diagnostics.Process"] = null,
        ["Microsoft.Win32.Registry"] = null,
        ["Microsoft.Win32.RegistryKey"] = null,
    };

    private static readonly HashSet<string> AllowedPInvokes =
    [
        "CreateFileW", "GetFileInformationByHandle", "GetFileInformationByHandleEx",
        "FindFirstFileExW", "FindNextFileW", "FindClose",
    ];

    private static MetadataReader Reader(System.Reflection.Assembly assembly, out PEReader pe)
    {
        pe = new PEReader(File.OpenRead(assembly.Location));
        return pe.GetMetadataReader();
    }

    [Fact]
    public void Inspector_assembly_does_not_reference_Executor()
    {
        var reader = Reader(typeof(StorageInspector).Assembly, out var pe);
        using (pe)
        {
            var names = reader.AssemblyReferences.Select(h => reader.GetString(reader.GetAssemblyReference(h).Name)).ToList();
            Assert.DoesNotContain(names, n => n.Contains("Executor", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Inspector_assembly_references_no_mutation_member()
    {
        var reader = Reader(typeof(StorageInspector).Assembly, out var pe);
        using (pe)
        {
            var offenders = new List<string>();
            foreach (var h in reader.MemberReferences)
            {
                var m = reader.GetMemberReference(h);
                if (m.Parent.Kind != HandleKind.TypeReference) continue;
                var t = reader.GetTypeReference((TypeReferenceHandle)m.Parent);
                var full = reader.GetString(t.Namespace) + "." + reader.GetString(t.Name);
                if (!BannedMembers.TryGetValue(full, out var banned)) continue;
                var name = reader.GetString(m.Name);
                if (banned is null || banned.Contains(name)) offenders.Add(full + "." + name);
            }
            Assert.Empty(offenders);
        }
    }

    [Fact]
    public void Win32ReadOnly_imports_only_read_calls()
    {
        var reader = Reader(typeof(MustBeCleaner.Infrastructure.Interop.Win32ReadOnly).Assembly, out var pe);
        using (pe)
        {
            var imports = new List<string>();
            foreach (var th in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(th);
                if (reader.GetString(type.Name) != nameof(MustBeCleaner.Infrastructure.Interop.Win32ReadOnly)) continue;
                foreach (var mh in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(mh);
                    var import = method.GetImport();
                    if (!import.Name.IsNil) imports.Add(reader.GetString(import.Name));
                }
            }
            Assert.NotEmpty(imports);
            Assert.All(imports, i => Assert.Contains(i, AllowedPInvokes));
        }
    }
}
