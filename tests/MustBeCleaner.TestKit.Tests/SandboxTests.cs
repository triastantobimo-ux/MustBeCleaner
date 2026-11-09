using MustBeCleaner.TestKit.Sandboxing;
using Xunit;

namespace MustBeCleaner.TestKit.Tests;

public class SandboxTests
{
    [Fact]
    public void Root_is_created_under_the_system_temp_directory_and_removed_on_dispose()
    {
        string root;
        using (var sandbox = new Sandbox())
        {
            root = sandbox.Root;
            Assert.True(Directory.Exists(root));
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), root, StringComparison.OrdinalIgnoreCase);
        }
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void WriteFile_creates_parent_folders_and_content_inside_root()
    {
        using var sandbox = new Sandbox();
        var path = sandbox.WriteFile(@"a\b\c.txt", "hello");
        Assert.Equal("hello", File.ReadAllText(path));
        Assert.StartsWith(sandbox.Root, path, StringComparison.OrdinalIgnoreCase);
    }

    // Guard test: the sandbox must refuse to touch anything outside its own temp root.
    [Theory]
    [InlineData(@"..\escape.txt")]
    [InlineData(@"a\..\..\escape.txt")]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData(@"\\server\share\x.txt")]
    [InlineData(@"\\?\C:\Windows\x.txt")]
    [InlineData(@"C:relative-to-drive-cwd.txt")]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_throws_when_a_path_escapes_the_sandbox_root(string relative)
    {
        using var sandbox = new Sandbox();
        Assert.Throws<SandboxEscapeException>(() => sandbox.Resolve(relative));
    }

    [Fact]
    public void Resolve_throws_for_a_sibling_folder_that_merely_shares_the_root_prefix()
    {
        using var sandbox = new Sandbox();
        var sibling = @"..\" + Path.GetFileName(sandbox.Root) + "-evil\\x.txt";
        Assert.Throws<SandboxEscapeException>(() => sandbox.Resolve(sibling));
    }

    [Fact]
    public void Mutating_operations_throw_on_escape_and_leave_the_outside_untouched()
    {
        using var sandbox = new Sandbox();
        var outside = Path.Combine(Path.GetDirectoryName(sandbox.Root)!, "mbc-outside-" + Guid.NewGuid().ToString("N") + ".txt");
        var relative = @"..\" + Path.GetFileName(outside);

        Assert.Throws<SandboxEscapeException>(() => sandbox.WriteFile(relative, "x"));
        Assert.Throws<SandboxEscapeException>(() => sandbox.CreateDirectory(relative));
        Assert.Throws<SandboxEscapeException>(() => sandbox.Delete(relative));
        Assert.False(File.Exists(outside));
    }

    [Fact]
    public void Junction_targets_must_be_inside_the_sandbox()
    {
        using var sandbox = new Sandbox();
        Assert.Throws<SandboxEscapeException>(() => sandbox.TryCreateJunction("link", @"..\"));
    }

    [Fact]
    public void Junction_is_removed_without_deleting_its_target_content()
    {
        string keep;
        using (var sandbox = new Sandbox())
        {
            sandbox.WriteFile(@"real\data.txt", "keep");
            keep = Path.Combine(sandbox.Root, "real", "data.txt");
            if (!sandbox.TryCreateJunction("link", "real")) return; // junction not permitted here
            Assert.True(File.Exists(Path.Combine(sandbox.Root, "link", "data.txt")));
            sandbox.Delete("link");
            Assert.True(File.Exists(keep));
        }
        Assert.False(File.Exists(keep));
    }

    [Fact]
    public void HardLink_shares_content_with_its_source()
    {
        using var sandbox = new Sandbox();
        sandbox.WriteFile("orig.bin", "payload");
        Assert.True(sandbox.TryCreateHardLink("link.bin", "orig.bin"));
        Assert.Equal("payload", File.ReadAllText(sandbox.Resolve("link.bin")));
        File.WriteAllText(sandbox.Resolve("orig.bin"), "changed");
        Assert.Equal("changed", File.ReadAllText(sandbox.Resolve("link.bin")));
    }

    [Fact]
    public void StandardTree_contains_case_variants_8dot3_unicode_and_empty_folders()
    {
        using var sandbox = new Sandbox();
        sandbox.AddStandardTree();

        Assert.True(File.Exists(sandbox.Resolve(@"case\Readme.TXT")));
        Assert.True(File.Exists(sandbox.Resolve(@"case-variant\readme.txt")));
        Assert.True(File.Exists(sandbox.Resolve(@"short\PROGRA~1\SETUP~1.EXE")));
        Assert.True(File.Exists(sandbox.Resolve("unicode\\r\u00e9sum\u00e9-\u65e5\u672c\u8a9e.txt")));
        Assert.True(Directory.Exists(sandbox.Resolve("empty")));
        Assert.Empty(Directory.GetFileSystemEntries(sandbox.Resolve("empty")));
    }
}
