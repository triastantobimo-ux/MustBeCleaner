using MustBeCleaner.Adapters;
using Xunit;

namespace MustBeCleaner.Adapters.Tests;

/// <summary>R8.2: nothing executable may enter through an Adapter definition.</summary>
public class AdapterRejectionTests
{
    private static AdapterLoadResult Load(string find, string replace) =>
        AdapterLoader.Parse(AdapterJson.Valid(j => j.Replace(find, replace)));

    private static void AssertRejected(AdapterLoadResult r, AdapterViolationCode code)
    {
        Assert.False(r.IsValid);
        Assert.Null(r.Adapter);
        Assert.Contains(r.Violations, v => v.Code == code);
    }

    [Theory]
    [InlineData("script")]
    [InlineData("command")]
    [InlineData("powershell")]
    [InlineData("javascript")]
    [InlineData("exec")]
    [InlineData("postInstall")]
    [InlineData("Executable")]
    public void Forbidden_field_names_are_rejected(string field) =>
        AssertRejected(Load("\"schemaVersion\": 1,", $"\"schemaVersion\": 1, \"{field}\": \"x\","), AdapterViolationCode.ExecutableField);

    [Fact]
    public void Forbidden_field_nested_in_fingerprint_is_rejected() =>
        AssertRejected(Load("\"appId\": \"Codex\",", "\"appId\": \"Codex\", \"command\": \"x\","), AdapterViolationCode.ExecutableField);

    [Fact]
    public void Unknown_field_is_rejected() =>
        AssertRejected(Load("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"note\": \"hi\","), AdapterViolationCode.UnknownField);

    [Theory]
    [InlineData("powershell -c Remove-Item x")]
    [InlineData("pwsh cleanup")]
    [InlineData("cmd /c del x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("cleanup.ps1")]
    [InlineData("run.bat")]
    [InlineData("tool.exe")]
    [InlineData("hook.js")]
    [InlineData("$(whoami)")]
    [InlineData("Invoke-Expression x")]
    public void Executable_references_in_text_fields_are_rejected(string value)
    {
        AssertRejected(Load("\"age-over-grace-period\"", $"\"{value}\""), AdapterViolationCode.ExecutableReference);
        AssertRejected(Load("\"owner-process-active\"", $"\"{value}\""), AdapterViolationCode.ExecutableReference);
        AssertRejected(Load("docs/LOCAL_STORAGE_REVIEW.md", value), AdapterViolationCode.ExecutableReference);
    }

    [Fact]
    public void Executable_in_allowed_path_is_rejected() =>
        AssertRejected(Load(@".codex\\.tmp", @".codex\\bin\\x.exe"), AdapterViolationCode.ExecutableReference);

    [Fact]
    public void Process_names_may_end_in_exe_because_they_are_matched_not_run() =>
        Assert.True(Load("\"Codex.exe\"", "\"Other App.exe\"").IsValid);

    [Theory]
    [InlineData(@"C:\\Users\\x")]
    [InlineData(@"\\\\server\\share")]
    [InlineData(@"..\\outside")]
    [InlineData(@".codex\\..\\..\\x")]
    [InlineData("/etc/x")]
    [InlineData(@".codex\\file:stream")]
    [InlineData(@".codex\\ca*che")]
    public void Unsafe_allowed_paths_are_rejected(string path) =>
        AssertRejected(Load(@".codex\\.tmp", path), AdapterViolationCode.UnsafePath);

    [Theory]
    [InlineData(@"Code\\User Data\\*\\Cache")]
    public void Whole_segment_wildcard_is_allowed(string path) =>
        Assert.True(Load(@".codex\\.tmp", path).IsValid);

    [Theory]
    [InlineData(@"..\\Other.exe")]
    [InlineData(@"dir\\Other.exe")]
    [InlineData("Oth/er.exe")]
    public void Process_names_must_be_bare_file_names(string name) =>
        AssertRejected(Load("\"Codex.exe\"", $"\"{name.Replace("\\", "\\\\")}\""), AdapterViolationCode.InvalidValue);

    [Fact]
    public void Missing_required_field_is_rejected() =>
        AssertRejected(Load("\"recoveryPolicy\": \"quarantine\"", "\"regenerationCostX\": 1"), AdapterViolationCode.MissingField);

    [Fact]
    public void Unknown_enum_value_is_rejected() =>
        AssertRejected(Load("\"temp\"", "\"magic\""), AdapterViolationCode.InvalidValue);

    [Fact]
    public void Unsupported_schema_version_is_rejected() =>
        AssertRejected(Load("\"schemaVersion\": 1", "\"schemaVersion\": 2"), AdapterViolationCode.UnsupportedSchemaVersion);

    [Fact]
    public void Malformed_json_is_reported_not_thrown() =>
        AssertRejected(AdapterLoader.Parse("{ not json"), AdapterViolationCode.MalformedJson);

    [Fact]
    public void Duplicate_property_is_rejected() =>
        AssertRejected(Load("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"ruleId\": \"dup\","), AdapterViolationCode.MalformedJson);

    [Fact]
    public void Wrong_type_is_rejected() =>
        AssertRejected(Load("\"blockers\": [\"owner-process-active\"]", "\"blockers\": \"owner-process-active\""), AdapterViolationCode.WrongType);
}
