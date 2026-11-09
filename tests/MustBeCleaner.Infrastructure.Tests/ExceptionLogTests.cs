using MustBeCleaner.Infrastructure.Journal;
using MustBeCleaner.Infrastructure.Logging;
using MustBeCleaner.TestKit.Sandboxing;

namespace MustBeCleaner.Infrastructure.Tests;

/// <summary>R16.2, R6.8: every skipped or denied Item gets an Exception_Log entry with a reason code.</summary>
public sealed class ExceptionLogTests : IDisposable
{
    private readonly Sandbox _sb = new();
    private readonly JournalStore _store;

    public ExceptionLogTests() => _store = JournalStore.Open(new JournalStoreOptions(_sb.CreateDirectory("data")));

    public void Dispose()
    {
        _store.Dispose();
        _sb.Dispose();
    }

    [Theory]
    [InlineData(ExceptionReason.AccessDenied, "ACCESS_DENIED")]
    [InlineData(ExceptionReason.SkippedActive, "SKIPPED_ACTIVE")]
    [InlineData(ExceptionReason.SkippedChanged, "SKIPPED_CHANGED")]
    [InlineData(ExceptionReason.DeniedPolicy, "DENIED_POLICY")]
    [InlineData(ExceptionReason.Unreadable, "UNREADABLE")]
    public void Record_stores_the_item_with_a_fixed_reason_code(ExceptionReason reason, string code)
    {
        var at = new DateTimeOffset(2025, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var log = new ExceptionLog(_store, clock: new FixedClock(at));
        log.Record("item-1", reason);
        var entry = Assert.Single(_store.ListExceptions());
        Assert.Equal(new ExceptionEntryRecord("item-1", code, at), entry);
    }

    [Fact]
    public void Unknown_reason_or_blank_item_is_refused_and_nothing_is_stored()
    {
        var log = new ExceptionLog(_store);
        Assert.Throws<ArgumentOutOfRangeException>(() => log.Record("item-1", (ExceptionReason)999));
        Assert.Throws<ArgumentException>(() => log.Record(" ", ExceptionReason.AccessDenied));
        Assert.Empty(_store.ListExceptions());
    }

    [Fact]
    public void Access_denied_is_logged_without_any_escalation_and_the_diagnostic_line_has_no_path()
    {
        var lines = new List<string>();
        var safe = new SafeLog(new DelegateSink(lines.Add));
        new ExceptionLog(_store, safe).Record("item-9", ExceptionReason.AccessDenied);
        var line = Assert.Single(lines);
        Assert.Contains("ACCESS_DENIED", line);
        Assert.Contains("item-9", line);
        Assert.Single(_store.ListExceptions());
    }

    private sealed class DelegateSink(Action<string> write) : ILogSink
    {
        public void Write(string line) => write(line);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
