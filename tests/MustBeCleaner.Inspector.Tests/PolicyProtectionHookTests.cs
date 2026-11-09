using MustBeCleaner.Domain;

namespace MustBeCleaner.Inspector.Tests;

/// <summary>The Inspector's default hook must report the Policy Core protection rules (R6.7).</summary>
public class PolicyProtectionHookTests
{
    private static readonly PolicyProtectionHook Hook = new();

    [Theory]
    [InlineData(@"C:\Windows\System32\kernel32.dll", false, "P-WINDOWS-CRITICAL")]
    [InlineData(@"C:\Program Files\Acme\cache.tmp", false, "P-PROGRAM-FILES")]
    [InlineData(@"D:\data\cache\payload.exe", false, "P-BINARY")]
    [InlineData(@"C:\Users\mika\AppData\Local", true, "P-NEVER-RECURSIVE-ROOT")]
    [InlineData(@"D:\data\repos\app\file.tmp", false, "P-USER-DATA")]
    [InlineData(@"D:\data\cache\store.db", false, "P-TRANSACTION-SET")]
    [InlineData(@"D:\data\cache\README", false, "P-EXTENSIONLESS-UNOWNED")]
    public void Policy_core_protection_rules_are_reported(string path, bool isDirectory, string ruleId)
    {
        var verdict = Hook.Evaluate(path, isDirectory);

        Assert.True(verdict.IsProtected);
        Assert.Equal(ruleId, verdict.RuleId);
    }

    [Theory]
    [InlineData(@"D:\data\cache\blob.txt", false)]
    [InlineData(@"D:\data\cache\blob.bin", false)]
    [InlineData(@"D:\data\cache\sub", true)]
    public void Ordinary_paths_are_not_protected(string path, bool isDirectory)
        => Assert.False(Hook.Evaluate(path, isDirectory).IsProtected);

    [Fact]
    public void User_exclusions_are_honoured()
    {
        var excluded = PathPolicy.Normalize(@"D:\keep").Path!.Value;
        var hook = new PolicyProtectionHook(new RuleSet("test", [excluded]));

        var verdict = hook.Evaluate(@"D:\keep\cache\blob.tmp", false);

        Assert.True(verdict.IsProtected);
        Assert.Equal("X-USER-EXCLUSION", verdict.RuleId);
    }

    [Theory]
    [InlineData(@"\\server\share\blob")]
    [InlineData("")]
    public void Forbidden_paths_are_protected(string path)
    {
        var verdict = Hook.Evaluate(path, false);

        Assert.True(verdict.IsProtected);
        Assert.Equal("F-FORBIDDEN-PATH", verdict.RuleId);
    }

    [Fact]
    public void Path_only_overload_treats_the_path_as_a_file()
        => Assert.Equal("P-BINARY", Hook.Evaluate(@"D:\data\cache\payload.exe").RuleId);
}
