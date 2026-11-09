using System.Text.Json;

namespace MustBeCleaner.Adapters;

/// <summary>Walks a JSON tree against the Adapter schema, collecting violations instead of throwing.</summary>
internal sealed class AdapterReader
{
    public List<AdapterViolation> Violations { get; } = [];

    private static readonly string[] TopFields =
    [
        "schemaVersion", "ruleId", "version", "sourceReference", "fingerprint", "allowedRelativePaths",
        "category", "trust", "blockers", "processes", "requiredEvidence", "regenerationCost", "recoveryPolicy",
    ];

    public Adapter? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            Add(AdapterViolationCode.WrongType, "$", "Document must be a JSON object.");
            return null;
        }

        ScanForbiddenFields(root, "$");
        CheckFields(root, "$", TopFields);

        var schemaVersion = Int(root, "schemaVersion", "$");
        if (schemaVersion is int v && v != AdapterLoader.SupportedSchemaVersion)
            Add(AdapterViolationCode.UnsupportedSchemaVersion, "$.schemaVersion", $"Only schemaVersion {AdapterLoader.SupportedSchemaVersion} is supported.");

        var ruleId = Str(root, "ruleId", "$");
        var version = Str(root, "version", "$");
        var source = Str(root, "sourceReference", "$");
        var fingerprint = ReadFingerprint(root);
        var paths = StrList(root, "allowedRelativePaths", "$", requireNonEmpty: false, StrKind.Path);
        var category = Enum<AdapterCategory>(root, "category", "$");
        var trust = Enum<AdapterTrust>(root, "trust", "$");
        var blockers = StrList(root, "blockers", "$", requireNonEmpty: false);
        var processes = ReadProcesses(root);
        var evidence = StrList(root, "requiredEvidence", "$", requireNonEmpty: false);
        var cost = Enum<RegenerationCost>(root, "regenerationCost", "$");
        var recovery = Enum<RecoveryPolicy>(root, "recoveryPolicy", "$");

        if (Violations.Count > 0) return null;
        return new Adapter(ruleId!, version!, source!, fingerprint!, paths!, category!.Value, trust!.Value,
            blockers!, processes!, evidence!, cost!.Value, recovery!.Value);
    }

    private AppFingerprint? ReadFingerprint(JsonElement root)
    {
        if (!Obj(root, "fingerprint", "$", out var fp)) return null;
        CheckFields(fp, "$.fingerprint", ["appId", "versions"]);
        var appId = Str(fp, "appId", "$.fingerprint");
        var versions = StrList(fp, "versions", "$.fingerprint", requireNonEmpty: true);
        return appId is null || versions is null ? null : new AppFingerprint(appId, versions);
    }

    private ProcessMapping? ReadProcesses(JsonElement root)
    {
        if (!Obj(root, "processes", "$", out var p)) return null;
        CheckFields(p, "$.processes", ["owners", "helpers"]);
        var owners = StrList(p, "owners", "$.processes", requireNonEmpty: false, StrKind.ProcessName);
        var helpers = StrList(p, "helpers", "$.processes", requireNonEmpty: false, StrKind.ProcessName);
        return owners is null || helpers is null ? null : new ProcessMapping(owners, helpers);
    }

    private enum StrKind { Text, Path, ProcessName }

    /// <summary>Flags execution-implying property names at any depth, including inside unknown fields.</summary>
    private void ScanForbiddenFields(JsonElement e, string path)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in e.EnumerateObject())
                {
                    if (ExecutableContent.IsForbiddenFieldName(prop.Name))
                        Add(AdapterViolationCode.ExecutableField, $"{path}.{prop.Name}", "Field implies execution; Adapters are data-only.");
                    ScanForbiddenFields(prop.Value, $"{path}.{prop.Name}");
                }
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in e.EnumerateArray()) ScanForbiddenFields(item, $"{path}[{i++}]");
                break;
        }
    }

    private void CheckValue(string value, string path, StrKind kind)
    {
        if (kind == StrKind.ProcessName)
        {
            if (value.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || value.Any(char.IsControl) || value.Length > 255)
                Add(AdapterViolationCode.InvalidValue, path, "Process name must be a bare image file name.");
            return;
        }
        if (ExecutableContent.LooksExecutable(value))
        {
            Add(AdapterViolationCode.ExecutableReference, path, "Value references a script, executable or interpreter.");
            return;
        }
        if (kind == StrKind.Path) CheckRelativePath(value, path);
    }

    private void CheckRelativePath(string value, string path)
    {
        var bad = Path.IsPathRooted(value) || value.StartsWith('/') || value.StartsWith('\\') || value.Contains(':');
        foreach (var seg in value.Split('\\', '/'))
        {
            if (seg == "..") bad = true;
            if ((seg.Contains('*') || seg.Contains('?')) && seg != "*") bad = true;
        }
        if (bad) Add(AdapterViolationCode.UnsafePath, path, "Path must be relative, without '..', drive, UNC, stream ':' or partial wildcards.");
    }

    private void CheckFields(JsonElement obj, string path, string[] allowed)
    {
        foreach (var prop in obj.EnumerateObject())
            if (!allowed.Contains(prop.Name, StringComparer.Ordinal) && !ExecutableContent.IsForbiddenFieldName(prop.Name))
                Add(AdapterViolationCode.UnknownField, $"{path}.{prop.Name}", "Field is not part of the Adapter schema.");
    }

    private bool Obj(JsonElement parent, string name, string path, out JsonElement value)
    {
        value = default;
        if (!Has(parent, name, path, out var e)) return false;
        if (e.ValueKind != JsonValueKind.Object)
        {
            Add(AdapterViolationCode.WrongType, $"{path}.{name}", "Expected an object.");
            return false;
        }
        value = e;
        return true;
    }

    private string? Str(JsonElement parent, string name, string path)
    {
        if (!Has(parent, name, path, out var e)) return null;
        if (e.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(e.GetString()))
        {
            Add(AdapterViolationCode.WrongType, $"{path}.{name}", "Expected a non-empty string.");
            return null;
        }
        var s = e.GetString()!;
        CheckValue(s, $"{path}.{name}", StrKind.Text);
        return s;
    }

    private int? Int(JsonElement parent, string name, string path)
    {
        if (!Has(parent, name, path, out var e)) return null;
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetInt32(out var i))
        {
            Add(AdapterViolationCode.WrongType, $"{path}.{name}", "Expected an integer.");
            return null;
        }
        return i;
    }

    private List<string>? StrList(JsonElement parent, string name, string path, bool requireNonEmpty, StrKind kind = StrKind.Text)
    {
        if (!Has(parent, name, path, out var e)) return null;
        if (e.ValueKind != JsonValueKind.Array)
        {
            Add(AdapterViolationCode.WrongType, $"{path}.{name}", "Expected an array of strings.");
            return null;
        }
        var list = new List<string>();
        var ok = true;
        var i = 0;
        foreach (var item in e.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                Add(AdapterViolationCode.WrongType, $"{path}.{name}[{i}]", "Expected a non-empty string.");
                ok = false;
            }
            else
            {
                list.Add(item.GetString()!);
                CheckValue(item.GetString()!, $"{path}.{name}[{i}]", kind);
            }
            i++;
        }
        if (requireNonEmpty && e.GetArrayLength() == 0)
        {
            Add(AdapterViolationCode.InvalidValue, $"{path}.{name}", "Array must not be empty.");
            return null;
        }
        return ok ? list : null;
    }

    private T? Enum<T>(JsonElement parent, string name, string path) where T : struct, Enum
    {
        var s = Str(parent, name, path);
        if (s is null) return null;
        foreach (var value in System.Enum.GetValues<T>())
            if (string.Equals(Kebab(value.ToString()), s, StringComparison.Ordinal)) return value;
        Add(AdapterViolationCode.InvalidValue, $"{path}.{name}", $"'{s}' is not one of: {string.Join(", ", System.Enum.GetValues<T>().Select(x => Kebab(x.ToString())))}.");
        return null;
    }

    private bool Has(JsonElement parent, string name, string path, out JsonElement value)
    {
        if (parent.TryGetProperty(name, out value)) return true;
        Add(AdapterViolationCode.MissingField, $"{path}.{name}", "Required field is missing.");
        return false;
    }

    private static string Kebab(string pascal) =>
        string.Concat(pascal.Select((c, i) => char.IsUpper(c) ? (i == 0 ? "" : "-") + char.ToLowerInvariant(c) : c.ToString()));

    private void Add(AdapterViolationCode code, string path, string message) => Violations.Add(new(code, path, message));
}
