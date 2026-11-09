using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MustBeCleaner.Infrastructure.Logging;

/// <summary>Receives finished log lines. Implementations only store them.</summary>
public interface ILogSink
{
    /// <summary>Stores one single-line log record.</summary>
    void Write(string line);
}

/// <summary>Settings for <see cref="SafeLog"/>.</summary>
public sealed record LogPolicy
{
    /// <summary>Whether <see cref="LogField.Path"/> values are written (scrubbed). Default false: paths are masked.</summary>
    public bool AllowPaths { get; init; }

    /// <summary>Longest path written; longer ones are cut.</summary>
    public int MaxPathLength { get; init; } = 512;
}

/// <summary>
/// One typed log field. The factories are the only way to build one, so there is no way to hand
/// the logger free text or a content payload (R16.3).
/// </summary>
public readonly record struct LogField
{
    internal LogField(FieldKind kind, string key, string? text, long number)
    {
        Kind = kind;
        Key = key;
        Text = text;
        Number = number;
    }

    internal FieldKind Kind { get; }
    internal string Key { get; }
    internal string? Text { get; }
    internal long Number { get; }

    /// <summary>An opaque identifier (Item ID, scan ID). Anything that is not a plain identifier is masked.</summary>
    public static LogField Id(string key, string value) => new(FieldKind.Id, key, value, 0);

    /// <summary>A reason code such as <c>ACCESS_DENIED</c>. Anything else is replaced by <c>INVALID_REASON</c>.</summary>
    public static LogField Reason(string key, string code) => new(FieldKind.Reason, key, code, 0);

    /// <summary>A path. Masked unless <see cref="LogPolicy.AllowPaths"/> is true; then scrubbed, query removed, length capped.</summary>
    public static LogField Path(string key, string path) => new(FieldKind.Path, key, path, 0);

    /// <summary>A number such as a count or a size.</summary>
    public static LogField Count(string key, long value) => new(FieldKind.Count, key, null, value);
}

internal enum FieldKind { Id, Reason, Path, Count }

/// <summary>
/// The only logging entry point. Writes one JSON line per event with identifiers, reason codes,
/// counts and (when allowed) scrubbed paths. It never writes file content, tokens or secrets (R16.3)
/// and never throws into the caller if the sink fails.
/// </summary>
public sealed partial class SafeLog(ILogSink sink, LogPolicy? policy = null, TimeProvider? clock = null)
{
    private readonly ILogSink _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    private readonly LogPolicy _policy = policy ?? new LogPolicy();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Writes one event. An invalid event name is replaced by <c>invalid-event</c>.</summary>
    public void Write(string eventName, params LogField[] fields)
    {
        try
        {
            var values = new Dictionary<string, object?>();
            foreach (var f in fields ?? [])
            {
                var key = f.Key ?? "";
                if (!SafeToken().IsMatch(key)) continue;
                values[key] = IsSensitiveKey(key) ? Redactor.Mask : Render(f);
            }
            var record = new Dictionary<string, object?>
            {
                ["t"] = _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                ["e"] = SafeToken().IsMatch(eventName ?? "") ? eventName : "invalid-event",
                ["f"] = values,
            };
            _sink.Write(JsonSerializer.Serialize(record));
        }
        catch (Exception)
        {
            // Logging must never break a cleanup operation.
        }
    }

    private object? Render(LogField f) => f.Kind switch
    {
        FieldKind.Count => f.Number,
        FieldKind.Id => f.Text is not null && SafeToken().IsMatch(f.Text) ? f.Text : Redactor.Mask,
        FieldKind.Reason => f.Text is not null && ReasonCode().IsMatch(f.Text) ? f.Text : "INVALID_REASON",
        FieldKind.Path => RenderPath(f.Text),
        _ => Redactor.Mask,
    };

    private string RenderPath(string? path)
    {
        if (!_policy.AllowPaths) return "[PATH]";
        var p = path ?? "";
        var q = p.IndexOf('?');
        if (q >= 0) p = p[..q];
        p = Redactor.Scrub(p).Replace('\n', ' ').Replace('\r', ' ');
        return p.Length > _policy.MaxPathLength ? p[.._policy.MaxPathLength] + "…[CUT]" : p;
    }

    private static bool IsSensitiveKey(string key) => SensitiveKey().IsMatch(key);

    [GeneratedRegex(@"^[A-Za-z0-9_.:\-]{1,128}$")]
    private static partial Regex SafeToken();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.\-]{0,63}$")]
    private static partial Regex ReasonCode();

    [GeneratedRegex(@"content|payload|body|data|text|token|secret|passw|pwd|credential|key|auth|sig|cookie", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveKey();
}
