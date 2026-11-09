using System.Text.Json;

namespace MustBeCleaner.Adapters;

/// <summary>
/// Loads Adapter definitions from JSON (R8.1) and rejects anything executable (R8.2).
/// Strict by construction: only the fields in <c>policies/adapter.schema.json</c> are accepted,
/// so an unknown field can never smuggle behaviour in.
/// </summary>
public static class AdapterLoader
{
    public const int SupportedSchemaVersion = 1;

    /// <summary>Parses one Adapter document. Never throws for bad input; inspect <see cref="AdapterLoadResult.Violations"/>.</summary>
    public static AdapterLoadResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (JsonException e)
        {
            return Rejected(new AdapterViolation(AdapterViolationCode.MalformedJson, "$", e.Message));
        }

        using (doc)
        {
            var reader = new AdapterReader();
            var adapter = reader.Read(doc.RootElement);
            return reader.Violations.Count == 0
                ? new AdapterLoadResult(adapter, [])
                : new AdapterLoadResult(null, reader.Violations);
        }
    }

    /// <summary>Reads and parses a file. IO failures surface as exceptions; content problems as violations.</summary>
    public static AdapterLoadResult LoadFile(string path) => Parse(File.ReadAllText(path));

    private static AdapterLoadResult Rejected(AdapterViolation v) => new(null, [v]);
}
