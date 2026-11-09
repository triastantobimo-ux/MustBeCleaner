using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace MustBeCleaner.Infrastructure.Journal;

/// <summary>
/// Persistence boundary: a SQLite (WAL) database inside the Data_Directory (R16.4, R16.5).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Opening is fail-closed: a missing, unwritable or full Data_Directory or Quarantine throws
/// <see cref="StorageUnavailableException"/>; there is no fallback location (R12.9).</item>
/// <item>Mutation records (action attempts, quarantine entries, restore attempts) need a
/// <see cref="JournalReceipt"/> from <see cref="AppendJournal"/>, written durably first (R16.1).</item>
/// <item><see cref="ExclusionRoots"/> lists what the scanner must skip (R12.10).</item>
/// </list>
/// </remarks>
public sealed class JournalStore : IDisposable
{
    /// <summary>File name of the database inside the Data_Directory.</summary>
    public const string DatabaseFileName = "journal.db";

    /// <summary>Schema version this build writes and understands.</summary>
    public const int CurrentSchemaVersion = Schema.CurrentVersion;

    private readonly SqliteConnection _connection;
    private readonly object _gate = new();

    private JournalStore(SqliteConnection connection, string dataDirectory, string? quarantineDirectory, int schemaVersion)
    {
        _connection = connection;
        DataDirectory = dataDirectory;
        QuarantineDirectory = quarantineDirectory;
        SchemaVersion = schemaVersion;
        ExclusionRoots = quarantineDirectory is null ? [dataDirectory] : [dataDirectory, quarantineDirectory];
    }

    /// <summary>Normalized absolute Data_Directory.</summary>
    public string DataDirectory { get; }

    /// <summary>Normalized absolute Quarantine destination, if configured.</summary>
    public string? QuarantineDirectory { get; }

    /// <summary>Schema version of the opened database.</summary>
    public int SchemaVersion { get; }

    /// <summary>Directories the scanner must exclude: Data_Directory and Quarantine (R12.10).</summary>
    public IReadOnlyList<string> ExclusionRoots { get; }

    /// <summary>True when <paramref name="path"/> is an exclusion root or lies below one (case-insensitive).</summary>
    public bool IsExcluded(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        foreach (var root in ExclusionRoots)
        {
            if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return true;
            if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>Opens (and on first use initializes) the Journal Store, or throws <see cref="StorageUnavailableException"/> (R12.9).</summary>
    public static JournalStore Open(JournalStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var dataDir = StorageGuard.Validate(StorageLocation.DataDirectory, options.DataDirectory, options.MinimumFreeBytes);
        string? quarantine = options.QuarantineDirectory is null
            ? null
            : StorageGuard.Validate(StorageLocation.Quarantine, options.QuarantineDirectory, 0);

        var dbPath = Path.Combine(dataDir, DatabaseFileName);
        var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate;Pooling=False;Foreign Keys=True");
        try
        {
            connection.Open();
            var wal = Scalar(connection, "PRAGMA journal_mode=WAL");
            if (!"wal".Equals(wal as string, StringComparison.OrdinalIgnoreCase))
                throw new StorageUnavailableException(StorageLocation.DataDirectory, StorageFailure.Unwritable, options.DataDirectory);
            Exec(connection, "PRAGMA synchronous=FULL");

            var version = Convert.ToInt32(Scalar(connection, "PRAGMA user_version"), CultureInfo.InvariantCulture);
            if (version > Schema.CurrentVersion)
                throw new StorageUnavailableException(StorageLocation.DataDirectory, StorageFailure.SchemaNewer, options.DataDirectory);
            if (version < 1)
            {
                using var tx = connection.BeginTransaction();
                Exec(connection, Schema.CreateV1, tx);
                Exec(connection, $"PRAGMA user_version = {Schema.CurrentVersion}", tx);
                tx.Commit();
                version = Schema.CurrentVersion;
            }
            return new JournalStore(connection, dataDir, quarantine, version);
        }
        catch (StorageUnavailableException)
        {
            connection.Dispose();
            throw;
        }
        catch (SqliteException e)
        {
            connection.Dispose();
            var failure = e.SqliteErrorCode is 11 or 26 ? StorageFailure.Corrupt : StorageFailure.Unwritable;
            throw new StorageUnavailableException(StorageLocation.DataDirectory, failure, options.DataDirectory, e);
        }
    }

    // ---- Journal (R16.1) -------------------------------------------------------------------

    /// <summary>
    /// Writes a Journal entry and returns only after it is committed with full sync. The returned
    /// receipt unlocks the matching mutation record.
    /// </summary>
    public JournalReceipt AppendJournal(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            var id = Convert.ToInt64(Scalar(_connection,
                "INSERT INTO Journal (ItemId, ManifestId, Stage, At, Detail) VALUES ($i,$m,$s,$a,$d); SELECT last_insert_rowid();",
                ("$i", entry.ItemId), ("$m", entry.ManifestId), ("$s", entry.Stage.ToString()),
                ("$a", Time(entry.At)), ("$d", entry.Detail)), CultureInfo.InvariantCulture);
            return new JournalReceipt(id, entry.ItemId, entry.Stage);
        }
    }

    /// <summary>Reads the Journal in write order.</summary>
    public IReadOnlyList<StoredJournalEntry> ReadJournal()
    {
        lock (_gate)
        {
            return Query("SELECT EntryId, ItemId, ManifestId, Stage, At, Detail FROM Journal ORDER BY EntryId",
                r => new StoredJournalEntry(r.GetInt64(0), new JournalEntry(
                    r.GetString(1), r.GetString(2), Enum.Parse<JournalStage>(r.GetString(3)), ParseTime(r.GetString(4)),
                    r.IsDBNull(5) ? null : r.GetString(5))));
        }
    }

    // ---- Mutation records: refused without a matching durable Journal entry ----------------

    /// <summary>Records an action attempt. The receipt must cover the same Item and stage (R16.1).</summary>
    public long RecordActionAttempt(ActionAttemptRecord record, JournalReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            RequireJournal(receipt, record.ItemId, record.Stage);
            return Convert.ToInt64(Scalar(_connection,
                "INSERT INTO ActionAttempt (JournalEntryId, ItemId, ManifestId, JournalStage, Outcome, Reason) VALUES ($j,$i,$m,$s,$o,$r); SELECT last_insert_rowid();",
                ("$j", receipt.EntryId), ("$i", record.ItemId), ("$m", record.ManifestId),
                ("$s", record.Stage.ToString()), ("$o", record.Outcome.ToString()), ("$r", record.Reason)),
                CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Records a Quarantine entry. The receipt must cover the same Item (R16.1).</summary>
    public void RecordQuarantineEntry(QuarantineEntryRecord record, JournalReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            RequireJournal(receipt, record.ItemId, null);
            Exec(_connection,
                "INSERT INTO QuarantineEntry (EntryId, JournalEntryId, ItemId, Location, Checksum, Metadata) VALUES ($e,$j,$i,$l,$c,$m)",
                null, ("$e", record.EntryId), ("$j", receipt.EntryId), ("$i", record.ItemId),
                ("$l", record.Location), ("$c", record.Checksum), ("$m", record.Metadata));
        }
    }

    /// <summary>Records a restore attempt. The receipt must cover the Item held by the Quarantine entry (R16.1).</summary>
    public long RecordRestoreAttempt(RestoreAttemptRecord record, JournalReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            var itemId = Scalar(_connection, "SELECT ItemId FROM QuarantineEntry WHERE EntryId=$e", ("$e", record.EntryId)) as string
                ?? throw new JournalRequiredException($"Quarantine entry '{record.EntryId}' does not exist.");
            RequireJournal(receipt, itemId, null);
            return Convert.ToInt64(Scalar(_connection,
                "INSERT INTO RestoreAttempt (JournalEntryId, EntryId, Status, Reason) VALUES ($j,$e,$s,$r); SELECT last_insert_rowid();",
                ("$j", receipt.EntryId), ("$e", record.EntryId), ("$s", record.Status), ("$r", record.Reason)),
                CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Reads action attempts in write order.</summary>
    public IReadOnlyList<StoredActionAttempt> ListActionAttempts()
    {
        lock (_gate)
        {
            return Query("SELECT AttemptId, JournalEntryId, ItemId, ManifestId, JournalStage, Outcome, Reason FROM ActionAttempt ORDER BY AttemptId",
                r => new StoredActionAttempt(r.GetInt64(0), r.GetInt64(1), new ActionAttemptRecord(
                    r.GetString(2), r.GetString(3), Enum.Parse<JournalStage>(r.GetString(4)),
                    Enum.Parse<ActionOutcome>(r.GetString(5)), r.IsDBNull(6) ? null : r.GetString(6))));
        }
    }

    /// <summary>Reads a Quarantine entry, or <c>null</c>.</summary>
    public QuarantineEntryRecord? GetQuarantineEntry(string entryId)
    {
        lock (_gate)
        {
            return Query("SELECT EntryId, ItemId, Location, Checksum, Metadata FROM QuarantineEntry WHERE EntryId=$e",
                r => new QuarantineEntryRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)),
                ("$e", entryId)).FirstOrDefault();
        }
    }

    /// <summary>Reads restore attempts for a Quarantine entry in write order.</summary>
    public IReadOnlyList<RestoreAttemptRecord> ListRestoreAttempts(string entryId)
    {
        lock (_gate)
        {
            return Query("SELECT EntryId, Status, Reason FROM RestoreAttempt WHERE EntryId=$e ORDER BY AttemptId",
                r => new RestoreAttemptRecord(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)),
                ("$e", entryId));
        }
    }

    // ---- Non-mutation records --------------------------------------------------------------

    /// <summary>Adds a scan run.</summary>
    public void Add(ScanRunRecord r) => Insert(
        "INSERT INTO ScanRun VALUES ($a,$b,$c,$d,$e,$f)",
        ("$a", r.ScanId), ("$b", Json(r.Scope)), ("$c", Time(r.StartedAt)), ("$d", r.Status), ("$e", r.PolicyVersion), ("$f", Json(r.Caveats)));

    /// <summary>Adds per-root coverage.</summary>
    public void Add(ScanCoverageRecord r) => Insert(
        "INSERT INTO ScanCoverage VALUES ($a,$b,$c,$d,$e,$f,$g)",
        ("$a", r.ScanId), ("$b", r.Root), ("$c", r.Status), ("$d", r.EntryCount), ("$e", r.ReparseSkipped), ("$f", r.AccessDenied), ("$g", r.DurationMs));

    /// <summary>Adds a discovered Item; an unavailable size is stored as NULL, never zero (R3.5).</summary>
    public void Add(DiscoveredItemRecord r) => Insert(
        "INSERT INTO DiscoveredItem VALUES ($a,$b,$c,$d,$e,$f,$g,$h)",
        ("$a", r.ItemId), ("$b", r.ScanId), ("$c", r.VolumeId), ("$d", r.FileId), ("$e", r.CanonicalPath),
        ("$f", r.SizeBytes), ("$g", r.ModifiedAt is { } m ? Time(m) : null), ("$h", r.Attributes));

    /// <summary>Adds Adapter ownership evidence.</summary>
    public void Add(AppOwnershipEvidenceRecord r) => Insert(
        "INSERT INTO AppOwnershipEvidence VALUES ($a,$b,$c,$d)",
        ("$a", r.ItemId), ("$b", r.AdapterRuleId), ("$c", r.AdapterVersion), ("$d", Json(r.Evidence)));

    /// <summary>Adds a rule version.</summary>
    public void Add(RuleVersionRecord r) => Insert(
        "INSERT INTO RuleVersion VALUES ($a,$b,$c,$d)", ("$a", r.RuleId), ("$b", r.Version), ("$c", r.SourceRef), ("$d", r.Hash));

    /// <summary>Adds a classification.</summary>
    public void Add(ClassificationRecord r) => Insert(
        "INSERT INTO Classification VALUES ($a,$b,$c,$d,$e,$f)",
        ("$a", r.ItemId), ("$b", r.Status.ToString()), ("$c", r.RuleId), ("$d", r.RuleVersion), ("$e", Json(r.Reasons)), ("$f", r.Conditions));

    /// <summary>Adds a duplicate group.</summary>
    public void Add(DuplicateGroupRecord r) => Insert(
        "INSERT INTO DuplicateGroup VALUES ($a,$b,$c,$d,$e)",
        ("$a", r.GroupId), ("$b", r.Kind.ToString()), ("$c", Json(r.Members)), ("$d", r.KeeperId), ("$e", r.Verified ? 1 : 0));

    /// <summary>Adds a Keeper decision.</summary>
    public void Add(KeeperDecisionRecord r) => Insert(
        "INSERT INTO KeeperDecision VALUES ($a,$b)", ("$a", r.GroupId), ("$b", r.KeeperItemId));

    /// <summary>Adds a cleanup Plan.</summary>
    public void Add(CleanupPlanRecord r) => Insert(
        "INSERT INTO CleanupPlan VALUES ($a,$b)", ("$a", r.PlanId), ("$b", r.Totals));

    /// <summary>Adds a Plan item.</summary>
    public void Add(PlanItemRecord r) => Insert(
        "INSERT INTO PlanItem VALUES ($a,$b,$c)", ("$a", r.PlanId), ("$b", r.ItemId), ("$c", r.EvidenceSnapshot));

    /// <summary>Adds an Exception_Log entry (R16.2). Carry a reason code only, never file content (R16.3).</summary>
    public void Add(ExceptionEntryRecord r) => Insert(
        "INSERT INTO ExceptionEntry (ItemId, ReasonCode, At) VALUES ($a,$b,$c)", ("$a", r.ItemId), ("$b", r.ReasonCode), ("$c", Time(r.At)));

    /// <summary>Reads a scan run, or <c>null</c>.</summary>
    public ScanRunRecord? GetScanRun(string scanId)
    {
        lock (_gate)
        {
            return Query("SELECT ScanId, ScopeJson, StartedAt, Status, PolicyVersion, CaveatsJson FROM ScanRun WHERE ScanId=$a",
                r => new ScanRunRecord(r.GetString(0), Strings(r.GetString(1)), ParseTime(r.GetString(2)), r.GetString(3), r.GetString(4), Strings(r.GetString(5))),
                ("$a", scanId)).FirstOrDefault();
        }
    }

    /// <summary>Reads a discovered Item, or <c>null</c>. A NULL size comes back as <c>null</c>, not zero.</summary>
    public DiscoveredItemRecord? GetDiscoveredItem(string itemId)
    {
        lock (_gate)
        {
            return Query("SELECT ItemId, ScanId, VolumeId, FileId, CanonicalPath, SizeBytes, ModifiedAt, Attributes FROM DiscoveredItem WHERE ItemId=$a",
                r => new DiscoveredItemRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetInt64(5), r.IsDBNull(6) ? null : ParseTime(r.GetString(6)), r.GetInt32(7)),
                ("$a", itemId)).FirstOrDefault();
        }
    }

    /// <summary>Reads the Exception_Log in write order.</summary>
    public IReadOnlyList<ExceptionEntryRecord> ListExceptions()
    {
        lock (_gate)
        {
            return Query("SELECT ItemId, ReasonCode, At FROM ExceptionEntry ORDER BY Id",
                r => new ExceptionEntryRecord(r.GetString(0), r.GetString(1), ParseTime(r.GetString(2))));
        }
    }

    // ---- Scan history retention (R16.6, R22.2, R15.6) --------------------------------------

    /// <summary>
    /// Lists scan runs with their start time and whether they hold an Item whose recovery is unfinished:
    /// Journal entries exist but none reached <c>SourceReleased</c>, or an action ended in <c>RecoveryRequired</c> (R15.6).
    /// </summary>
    public IReadOnlyList<ScanHistoryEntry> ListScanHistory()
    {
        lock (_gate)
        {
            return Query($"SELECT s.ScanId, s.StartedAt, EXISTS(SELECT 1 FROM DiscoveredItem d WHERE d.ScanId=s.ScanId AND {UnfinishedItem}) FROM ScanRun s",
                r => new ScanHistoryEntry(r.GetString(0), ParseTime(r.GetString(1)), r.GetInt32(2) == 1));
        }
    }

    /// <summary>
    /// Deletes scan runs and their dependent rows in one transaction. Runs that hold an unfinished recovery
    /// Item are skipped, whatever the caller asked (R15.6). The Journal itself is never touched. Returns the number deleted.
    /// </summary>
    public int DeleteScanRuns(IReadOnlyCollection<string> scanIds)
    {
        ArgumentNullException.ThrowIfNull(scanIds);
        lock (_gate)
        {
            var deleted = 0;
            using var tx = _connection.BeginTransaction();
            foreach (var id in scanIds)
            {
                var held = Convert.ToInt64(Scalar(_connection,
                    $"SELECT COUNT(*) FROM DiscoveredItem d WHERE d.ScanId=$s AND {UnfinishedItem}", ("$s", id)), CultureInfo.InvariantCulture);
                if (held > 0) continue;
                var items = "(SELECT ItemId FROM DiscoveredItem WHERE ScanId=$s)";
                Exec(_connection, $"DELETE FROM Classification WHERE ItemId IN {items}", tx, ("$s", id));
                Exec(_connection, $"DELETE FROM AppOwnershipEvidence WHERE ItemId IN {items}", tx, ("$s", id));
                Exec(_connection, "DELETE FROM DiscoveredItem WHERE ScanId=$s", tx, ("$s", id));
                Exec(_connection, "DELETE FROM ScanCoverage WHERE ScanId=$s", tx, ("$s", id));
                using var cmd = _connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM ScanRun WHERE ScanId=$s";
                cmd.Parameters.AddWithValue("$s", id);
                deleted += cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return deleted;
        }
    }

    private const string UnfinishedItem =
        "((EXISTS(SELECT 1 FROM Journal j WHERE j.ItemId=d.ItemId) AND NOT EXISTS(SELECT 1 FROM Journal j WHERE j.ItemId=d.ItemId AND j.Stage='SourceReleased'))" +
        " OR EXISTS(SELECT 1 FROM ActionAttempt a WHERE a.ItemId=d.ItemId AND a.Outcome='RecoveryRequired'))";

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate) { _connection.Dispose(); }
    }

    // ---- helpers ---------------------------------------------------------------------------

    private void RequireJournal(JournalReceipt? receipt, string itemId, JournalStage? stage)
    {
        if (receipt is null)
            throw new JournalRequiredException("A durable Journal entry must be written before this record (R16.1).");
        if (!receipt.ItemId.Equals(itemId, StringComparison.Ordinal) || (stage is { } s && receipt.Stage != s))
            throw new JournalRequiredException("The Journal receipt does not cover this Item and stage (R16.1).");
        var found = Scalar(_connection, "SELECT COUNT(*) FROM Journal WHERE EntryId=$e AND ItemId=$i AND ($s IS NULL OR Stage=$s)",
            ("$e", receipt.EntryId), ("$i", itemId), ("$s", stage?.ToString()));
        if (Convert.ToInt64(found, CultureInfo.InvariantCulture) != 1)
            throw new JournalRequiredException($"No durable Journal entry {receipt.EntryId} exists in this store (R16.1).");
    }

    private void Insert(string sql, params (string Name, object? Value)[] p)
    {
        lock (_gate) { Exec(_connection, sql, null, p); }
    }

    private List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] p)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, p);
        using var reader = cmd.ExecuteReader();
        var list = new List<T>();
        while (reader.Read()) list.Add(map(reader));
        return list;
    }

    private static void Exec(SqliteConnection c, string sql, SqliteTransaction? tx = null, params (string Name, object? Value)[] p)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        Bind(cmd, p);
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection c, string sql, params (string Name, object? Value)[] p)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, p);
        var value = cmd.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    private static void Bind(SqliteCommand cmd, (string Name, object? Value)[] p)
    {
        foreach (var (name, value) in p) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static string Json(IReadOnlyList<string> values) => JsonSerializer.Serialize(values);
    private static string[] Strings(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
    private static string Time(DateTimeOffset t) => t.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseTime(string s) => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
