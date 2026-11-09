using MustBeCleaner.Infrastructure.Journal;

namespace MustBeCleaner.Infrastructure.Logging;

/// <summary>Why an Item was skipped or denied (R16.2). Only these codes can be recorded, so no free text or content gets in (R16.3).</summary>
public enum ExceptionReason
{
    /// <summary>Permission denied; no escalation is attempted (R6.8).</summary>
    AccessDenied,
    /// <summary>The owning application is active (R11.4).</summary>
    SkippedActive,
    /// <summary>The Item changed since preview (R11.3).</summary>
    SkippedChanged,
    /// <summary>Denied by policy (R11.6).</summary>
    DeniedPolicy,
    /// <summary>The file could not be read (R18.11).</summary>
    Unreadable,
    /// <summary>A reparse point was not followed (R7).</summary>
    ReparseSkipped,
}

/// <summary>
/// Exception_Log policy: every skipped or denied Item is stored with a fixed reason code and a
/// timestamp (R16.2, R6.8), and a content-free diagnostic line is written through <see cref="SafeLog"/>.
/// It performs no retry or escalation.
/// </summary>
public sealed class ExceptionLog(JournalStore store, SafeLog? log = null, TimeProvider? clock = null)
{
    private readonly JournalStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Stable text code for <paramref name="reason"/>, for example <c>ACCESS_DENIED</c>.</summary>
    public static string Code(ExceptionReason reason) => reason switch
    {
        ExceptionReason.AccessDenied => "ACCESS_DENIED",
        ExceptionReason.SkippedActive => "SKIPPED_ACTIVE",
        ExceptionReason.SkippedChanged => "SKIPPED_CHANGED",
        ExceptionReason.DeniedPolicy => "DENIED_POLICY",
        ExceptionReason.Unreadable => "UNREADABLE",
        ExceptionReason.ReparseSkipped => "REPARSE_SKIPPED",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown exception reason."),
    };

    /// <summary>Records one skipped or denied Item.</summary>
    /// <exception cref="ArgumentException"><paramref name="itemId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a defined reason.</exception>
    public void Record(string itemId, ExceptionReason reason)
    {
        var code = Code(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        _store.Add(new ExceptionEntryRecord(itemId, code, _clock.GetUtcNow()));
        log?.Write("item-exception", LogField.Id("itemId", itemId), LogField.Reason("reason", code));
    }
}
