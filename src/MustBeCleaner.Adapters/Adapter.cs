namespace MustBeCleaner.Adapters;

/// <summary>What kind of data an Adapter location holds (R8.1 "kategori").</summary>
public enum AdapterCategory { Cache, Temp, BuildCache, Log, UserData, Binary, Other }

/// <summary>
/// Trust level of the Adapter. <see cref="InventoryOnly"/> is the initial state for every
/// Adapter until its rule is validated (R8.3).
/// </summary>
public enum AdapterTrust { InventoryOnly, Validated }

/// <summary>Cost of regenerating the data once it is gone (R8.1 "biaya regenerasi").</summary>
public enum RegenerationCost { Low, Medium, High, Unknown }

/// <summary>How a removed item can be recovered (R8.1 "kebijakan recovery").</summary>
public enum RecoveryPolicy { Quarantine, RecycleBin, NotRecoverable }

/// <summary>Application identity and the exact versions this Adapter was written for (R8.1, R8.13).</summary>
public sealed record AppFingerprint(string AppId, IReadOnlyList<string> Versions);

/// <summary>
/// Process image names used to decide if the app is idle. Names are matched, never launched (R8.2).
/// </summary>
public sealed record ProcessMapping(IReadOnlyList<string> Owners, IReadOnlyList<string> Helpers);

/// <summary>
/// Declarative, data-only Adapter definition (R8.1). Contains no executable content (R8.2);
/// <see cref="AdapterLoader"/> is the only way to obtain one.
/// </summary>
public sealed record Adapter(
    string RuleId,
    string Version,
    string SourceReference,
    AppFingerprint Fingerprint,
    IReadOnlyList<string> AllowedRelativePaths,
    AdapterCategory Category,
    AdapterTrust Trust,
    IReadOnlyList<string> Blockers,
    ProcessMapping Processes,
    IReadOnlyList<string> RequiredEvidence,
    RegenerationCost RegenerationCost,
    RecoveryPolicy RecoveryPolicy);

/// <summary>Why a document was rejected.</summary>
public enum AdapterViolationCode
{
    MalformedJson,
    UnsupportedSchemaVersion,
    MissingField,
    WrongType,
    UnknownField,
    /// <summary>A field whose name implies execution (script, command, ...). R8.2.</summary>
    ExecutableField,
    /// <summary>A string value that references a script, executable or interpreter. R8.2.</summary>
    ExecutableReference,
    InvalidValue,
    UnsafePath,
}

/// <summary>One reason a document was rejected, with the JSON path it applies to.</summary>
public sealed record AdapterViolation(AdapterViolationCode Code, string Path, string Message)
{
    public override string ToString() => $"{Code} at {Path}: {Message}";
}

/// <summary>Outcome of loading: either an <see cref="Adapter"/> or a non-empty violation list.</summary>
public sealed record AdapterLoadResult(Adapter? Adapter, IReadOnlyList<AdapterViolation> Violations)
{
    public bool IsValid => Adapter is not null && Violations.Count == 0;
}
