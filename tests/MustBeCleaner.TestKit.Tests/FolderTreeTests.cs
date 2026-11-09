using MustBeCleaner.TestKit.Pbt;
using MustBeCleaner.TestKit.Sandboxing;
using Xunit;

namespace MustBeCleaner.TestKit.Tests;

public class FolderTreeTests
{
    [MbcProperty(98, "Sample: generated trees materialize fully inside the sandbox", Arbitrary = new[] { typeof(FolderTreeArbitrary) })]
    public bool Generated_trees_materialize_inside_the_sandbox(FolderTree tree)
    {
        using var sandbox = new Sandbox();
        sandbox.Materialize(tree);
        return tree.Entries.All(e => e.IsDirectory
            ? Directory.Exists(sandbox.Resolve(e.Path))
            : File.Exists(sandbox.Resolve(e.Path)));
    }

    [Fact]
    public void Generated_trees_use_only_relative_paths_with_no_traversal()
    {
        PbtConfig.Check(FsCheck.Fluent.Prop.ForAll(FolderTreeArbitrary.Trees(), t =>t.Entries.All(e => !Path.IsPathRooted(e.Path) && !e.Path.Split('\\').Contains(".."))));
    }

    [Fact]
    public void Generator_covers_unicode_8dot3_and_empty_folders_across_many_samples()
    {
        var entries = FsCheck.Fluent.Gen.Sample(FolderTreeArbitrary.Generator(), 200).SelectMany(t => t.Entries).ToList();
        Assert.Contains(entries, e => e.Path.Any(c => c > 127));
        Assert.Contains(entries, e => e.Path.Contains('~'));
        Assert.Contains(entries, e => e.IsDirectory && !entries.Any(o => o.Path.StartsWith(e.Path + "\\", StringComparison.Ordinal)));
    }
}


