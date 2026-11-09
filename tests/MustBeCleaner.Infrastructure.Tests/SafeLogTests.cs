using MustBeCleaner.Infrastructure.Logging;
using MustBeCleaner.TestKit.Sandboxing;

namespace MustBeCleaner.Infrastructure.Tests;

/// <summary>R16.3: logs never carry file content, tokens or secrets. Public interface only.</summary>
public sealed class SafeLogTests
{
    private sealed class ListSink : ILogSink
    {
        public List<string> Lines { get; } = [];
        public void Write(string line) => Lines.Add(line);
    }

    private static (SafeLog Log, ListSink Sink) Make(LogPolicy? policy = null)
    {
        var sink = new ListSink();
        return (new SafeLog(sink, policy), sink);
    }

    [Fact]
    public void Identifiers_reason_codes_and_counts_are_written()
    {
        var (log, sink) = Make();
        log.Write("item-skipped", LogField.Id("itemId", "item-42"), LogField.Reason("reason", "ACCESS_DENIED"), LogField.Count("n", 7));
        var line = Assert.Single(sink.Lines);
        Assert.Contains("item-skipped", line);
        Assert.Contains("item-42", line);
        Assert.Contains("ACCESS_DENIED", line);
    }

    [Theory]
    [InlineData("Authorization: Bearer abcdef1234567890abcdef", "abcdef1234567890abcdef")]
    [InlineData("password=hunter2hunter2", "hunter2hunter2")]
    [InlineData("api_key: sk-AAAAAAAAAAAAAAAAAAAAAAAA", "AAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz0123456789", "ghp_abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData("AKIAABCDEFGHIJKLMNOP", "AKIAABCDEFGHIJKLMNOP")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc123SIGnature", "eyJzdWIiOiIxMjM0NTY3ODkwIn0")]
    [InlineData("https://user:s3cretPass@host/x", "s3cretPass")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nMIIEvQIBADANBg\n-----END RSA PRIVATE KEY-----", "MIIEvQIBADANBg")]
    public void Secret_shaped_values_are_redacted_everywhere(string input, string leaked)
    {
        Assert.DoesNotContain(leaked, Redactor.Scrub(input));

        var (log, sink) = Make(new LogPolicy { AllowPaths = true });
        log.Write("x", LogField.Path("p", "C:\\a\\" + input.Replace('\n', ' ')));
        Assert.DoesNotContain(leaked, Assert.Single(sink.Lines));
    }

    [Fact]
    public void Unsafe_identifier_values_are_replaced_not_logged()
    {
        var (log, sink) = Make();
        log.Write("x", LogField.Id("itemId", "line one\nline two with file content"));
        var line = Assert.Single(sink.Lines);
        Assert.DoesNotContain("file content", line);
        Assert.Contains("[REDACTED]", line);
    }

    [Theory]
    [InlineData("content")]
    [InlineData("fileContent")]
    [InlineData("payload")]
    [InlineData("body")]
    [InlineData("token")]
    [InlineData("clientSecret")]
    [InlineData("password")]
    [InlineData("credential")]
    public void Fields_named_like_content_or_secrets_never_carry_their_value(string key)
    {
        var (log, sink) = Make(new LogPolicy { AllowPaths = true });
        log.Write("x", LogField.Id(key, "harmless-looking"), LogField.Path(key + "2", "C:\\data\\harmless-path"));
        var line = Assert.Single(sink.Lines);
        Assert.DoesNotContain("harmless-looking", line);
        Assert.DoesNotContain("harmless-path", line);
    }

    [Fact]
    public void Invalid_reason_code_is_not_logged_verbatim()
    {
        var (log, sink) = Make();
        log.Write("x", LogField.Reason("reason", "the file said: my secret diary"));
        var line = Assert.Single(sink.Lines);
        Assert.DoesNotContain("diary", line);
    }

    [Fact]
    public void Paths_are_hidden_by_default_and_logged_scrubbed_only_when_allowed()
    {
        var (hidden, hiddenSink) = Make();
        hidden.Write("x", LogField.Path("p", @"C:\Users\a\file.txt"));
        Assert.DoesNotContain("file.txt", Assert.Single(hiddenSink.Lines));

        var (allowed, allowedSink) = Make(new LogPolicy { AllowPaths = true });
        allowed.Write("x", LogField.Path("p", @"C:\Users\a\file.txt?sig=topsecretvalue1"));
        var line = Assert.Single(allowedSink.Lines);
        Assert.Contains("file.txt", line);
        Assert.DoesNotContain("topsecretvalue1", line);
    }

    [Fact]
    public void Oversized_path_is_truncated()
    {
        var (log, sink) = Make(new LogPolicy { AllowPaths = true });
        log.Write("x", LogField.Path("p", @"C:\" + new string('a', 5000)));
        Assert.True(Assert.Single(sink.Lines).Length < 2000);
    }

    [Fact]
    public void Event_name_and_field_keys_cannot_inject_content()
    {
        var (log, sink) = Make();
        log.Write("evt\nforged", LogField.Id("k\"x", "v"));
        var line = Assert.Single(sink.Lines);
        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain("forged", line);
    }

    [Fact]
    public void A_failing_sink_does_not_throw_into_the_caller()
    {
        var log = new SafeLog(new ThrowingSink());
        log.Write("x");
    }

    private sealed class ThrowingSink : ILogSink
    {
        public void Write(string line) => throw new IOException("disk full");
    }

    [Fact]
    public void File_sink_writes_only_inside_the_data_directory_and_rolls_within_bounds()
    {
        using var sb = new Sandbox();
        var data = sb.CreateDirectory("data");
        var sink = new FileLogSink(data, maxFileBytes: 400, maxFiles: 3);
        var log = new SafeLog(sink);
        for (var i = 0; i < 200; i++) log.Write("tick", LogField.Count("i", i));

        var logs = Path.Combine(data, FileLogSink.DirectoryName);
        var files = Directory.GetFiles(logs);
        Assert.InRange(files.Length, 1, 3);
        Assert.All(files, f => Assert.True(new FileInfo(f).Length <= 400 + 200));
        Assert.Equal(new[] { "data" }, Directory.GetFileSystemEntries(sb.Root).Select(Path.GetFileName));
        Assert.Equal(new[] { FileLogSink.DirectoryName }, Directory.GetDirectories(data).Select(Path.GetFileName));
    }

    [Fact]
    public void File_sink_refuses_a_missing_data_directory_and_does_not_create_it()
    {
        using var sb = new Sandbox();
        var missing = Path.Combine(sb.Root, "nope");
        Assert.Throws<DirectoryNotFoundException>(() => new FileLogSink(missing));
        Assert.False(Directory.Exists(missing));
    }
}
