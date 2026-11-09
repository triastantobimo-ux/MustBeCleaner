using System.Text;

namespace MustBeCleaner.Infrastructure.Logging;

/// <summary>
/// Appends log lines to <c>logs/mustbecleaner.log</c> inside the Data_Directory and rolls the file
/// at a size limit, keeping a fixed number of files so logs cannot grow without bound (R22.2).
/// </summary>
public sealed class FileLogSink : ILogSink
{
    /// <summary>Folder inside the Data_Directory that holds the log files.</summary>
    public const string DirectoryName = "logs";

    private const string BaseName = "mustbecleaner";
    private readonly string _dir;
    private readonly long _maxFileBytes;
    private readonly int _maxFiles;
    private readonly object _gate = new();

    /// <summary>Opens the sink. The Data_Directory must exist; it is never created (R12.9).</summary>
    /// <exception cref="DirectoryNotFoundException">The Data_Directory does not exist.</exception>
    public FileLogSink(string dataDirectory, long maxFileBytes = 1024 * 1024, int maxFiles = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFileBytes, 64);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        var data = Path.GetFullPath(dataDirectory);
        if (!Directory.Exists(data)) throw new DirectoryNotFoundException($"Data_Directory not found: {data}");
        _dir = Path.Combine(data, DirectoryName);
        _maxFileBytes = maxFileBytes;
        _maxFiles = maxFiles;
        Directory.CreateDirectory(_dir);
    }

    /// <inheritdoc />
    public void Write(string line)
    {
        lock (_gate)
        {
            var current = Path.Combine(_dir, BaseName + ".log");
            var bytes = Encoding.UTF8.GetByteCount(line) + 2;
            if (File.Exists(current) && new FileInfo(current).Length + bytes > _maxFileBytes) Roll(current);
            File.AppendAllText(current, line.Replace('\n', ' ').Replace('\r', ' ') + Environment.NewLine, Encoding.UTF8);
        }
    }

    private void Roll(string current)
    {
        if (_maxFiles == 1)
        {
            File.Delete(current);
            return;
        }
        File.Delete(Archive(_maxFiles - 1));
        for (var i = _maxFiles - 2; i >= 1; i--)
            if (File.Exists(Archive(i))) File.Move(Archive(i), Archive(i + 1));
        File.Move(current, Archive(1));
    }

    private string Archive(int n) => Path.Combine(_dir, $"{BaseName}.{n}.log");
}
