using System.Xml.Linq;
using Xunit;

namespace MustBeCleaner.TestKit.Tests;

/// <summary>Split-Privilege guard (architecture_selection.md invariant 1): only Executor may mutate; Inspector side must not reach it.</summary>
public class SolutionStructureTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MustBeCleaner.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("MustBeCleaner.sln not found above test output.");
    }

    private static HashSet<string> TransitiveReferences(string project)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>([project]);
        while (stack.Count > 0)
        {
            var name = stack.Pop();
            var file = Path.Combine(RepoRoot(), "src", $"MustBeCleaner.{name}", $"MustBeCleaner.{name}.csproj");
            foreach (var r in XDocument.Load(file).Descendants("ProjectReference"))
            {
                var dep = Path.GetFileNameWithoutExtension(((string)r.Attribute("Include")!).Replace('\\', '/'))["MustBeCleaner.".Length..];
                if (seen.Add(dep)) stack.Push(dep);
            }
        }
        return seen;
    }

    [Theory]
    [InlineData("Inspector")]
    [InlineData("Duplicates")]
    [InlineData("Adapters")]
    [InlineData("Domain")]
    [InlineData("Infrastructure")]
    public void Only_Executor_side_code_may_depend_on_Executor(string project)
    {
        Assert.DoesNotContain("Executor", TransitiveReferences(project));
    }

    [Fact]
    public void Domain_PolicyCore_has_no_project_dependencies()
    {
        Assert.Empty(TransitiveReferences("Domain"));
    }

    [Fact]
    public void All_expected_projects_are_in_the_solution()
    {
        var sln = File.ReadAllText(Path.Combine(RepoRoot(), "MustBeCleaner.sln"));
        foreach (var p in new[] { "Domain", "Adapters", "Inspector", "Duplicates", "Executor", "Infrastructure", "TestKit", "TestKit.Tests" })
            Assert.Contains($"MustBeCleaner.{p}.csproj", sln);
    }
}

