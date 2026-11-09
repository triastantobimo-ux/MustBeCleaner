using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MustBeCleaner.Infrastructure.Interop;

/// <summary>One name returned by <see cref="Win32ReadOnly.ListDirectory"/>.</summary>
/// <param name="Name">File name only.</param>
/// <param name="Attributes">Raw Win32 attribute bits.</param>
/// <param name="ReparseTag">Reparse tag; meaningful only when the reparse attribute bit is set.</param>
/// <param name="Size">Logical size in bytes as reported by the directory entry.</param>
/// <param name="LastWriteFileTime">Last write time as a FILETIME tick count.</param>
public readonly record struct RawDirectoryEntry(string Name, uint Attributes, uint ReparseTag, long Size, long LastWriteFileTime);

/// <summary>Result of listing one directory. <see cref="ErrorCode"/> is 0 on success, otherwise the Win32 error.</summary>
public sealed record RawDirectoryListing(IReadOnlyList<RawDirectoryEntry> Entries, int ErrorCode)
{
    /// <summary>True when the directory was listed completely.</summary>
    public bool Succeeded => ErrorCode == 0;
}

/// <summary>Object identity read from an open handle: volume serial plus 128-bit file ID, with metadata.</summary>
public readonly record struct RawObjectId(
    ulong VolumeSerial, ulong FileIdLow, ulong FileIdHigh,
    uint LinkCount, uint Attributes, long Size, long LastWriteFileTime);

/// <summary>Result of <see cref="Win32ReadOnly.ReadObjectId"/>: an identity or a Win32 error code.</summary>
public sealed record RawObjectIdResult(RawObjectId? Id, int ErrorCode);

/// <summary>
/// Read-only Win32 metadata calls ported from the verified identity-binding approach
/// (docs/research/identity-binding-spike.md; ADR 0050). Opens use attribute-only access with
/// <c>FILE_FLAG_OPEN_REPARSE_POINT</c>, so no data is read, no final reparse point is followed,
/// and nothing is written. This type must never gain a mutating API (R1.3).
/// </summary>
public static class Win32ReadOnly
{
    /// <summary>Win32 ERROR_FILE_NOT_FOUND.</summary>
    public const int ErrorFileNotFound = 2;
    /// <summary>Win32 ERROR_PATH_NOT_FOUND.</summary>
    public const int ErrorPathNotFound = 3;
    /// <summary>Win32 ERROR_ACCESS_DENIED.</summary>
    public const int ErrorAccessDenied = 5;

    /// <summary>Attribute bit for a directory.</summary>
    public const uint AttributeDirectory = 0x10;
    /// <summary>Attribute bit for a reparse point.</summary>
    public const uint AttributeReparsePoint = 0x400;

    private const uint FileReadAttributes = 0x80;
    private const uint ShareAll = 7;
    private const uint OpenExisting = 3;
    private const uint FlagBackupSemantics = 0x02000000;
    private const uint FlagOpenReparsePoint = 0x00200000;
    private const int FileIdInfoClass = 18;
    private const int FindExInfoBasic = 1;
    private const int FindExSearchNameMatch = 0;
    private const int FindFirstExLargeFetch = 2;
    private const int ErrorNoMoreFiles = 18;
    private static readonly IntPtr InvalidFindHandle = new(-1);

    /// <summary>Lists the names in <paramref name="fullPath"/> without following any entry. Never throws for OS errors.</summary>
    public static RawDirectoryListing ListDirectory(string fullPath)
    {
        var pattern = @"\\?\" + fullPath.TrimEnd('\\') + @"\*";
        var entries = new List<RawDirectoryEntry>();
        var handle = FindFirstFileExW(pattern, FindExInfoBasic, out var data, FindExSearchNameMatch, IntPtr.Zero, FindFirstExLargeFetch);
        if (handle == InvalidFindHandle) return new RawDirectoryListing(entries, Marshal.GetLastPInvokeError());
        try
        {
            do
            {
                if (data.cFileName is "." or "..") continue;
                var reparse = (data.dwFileAttributes & AttributeReparsePoint) != 0 ? data.dwReserved0 : 0;
                entries.Add(new RawDirectoryEntry(data.cFileName, data.dwFileAttributes, reparse,
                    ((long)data.nFileSizeHigh << 32) | data.nFileSizeLow, data.ftLastWriteTime));
            }
            while (FindNextFileW(handle, out data));

            var end = Marshal.GetLastPInvokeError();
            return new RawDirectoryListing(entries, end == ErrorNoMoreFiles ? 0 : end);
        }
        finally { FindClose(handle); }
    }

    /// <summary>Reads the object identity of <paramref name="fullPath"/> from an attribute-only handle. Never follows a final reparse point.</summary>
    public static RawObjectIdResult ReadObjectId(string fullPath)
    {
        using var handle = CreateFileW(@"\\?\" + fullPath.TrimEnd('\\'), FileReadAttributes, ShareAll, IntPtr.Zero,
            OpenExisting, FlagBackupSemantics | FlagOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid) return new RawObjectIdResult(null, Marshal.GetLastPInvokeError());
        if (!GetFileInformationByHandle(handle, out var info)) return new RawObjectIdResult(null, Marshal.GetLastPInvokeError());
        if (!GetFileInformationByHandleEx(handle, FileIdInfoClass, out var id, (uint)Marshal.SizeOf<FileIdInfo>()))
            return new RawObjectIdResult(null, Marshal.GetLastPInvokeError());

        var raw = new RawObjectId(id.VolumeSerialNumber, id.FileIdLow, id.FileIdHigh, info.nNumberOfLinks,
            info.dwFileAttributes, ((long)info.nFileSizeHigh << 32) | info.nFileSizeLow, info.ftLastWriteTime);
        return new RawObjectIdResult(raw, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo { public ulong VolumeSerialNumber; public ulong FileIdLow; public ulong FileIdHigh; }

    // Pack = 4: FILETIME is two DWORDs, so the 64-bit fields are only 4-aligned (spike finding).
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct ByHandleFileInformation
    {
        public uint dwFileAttributes;
        public long ftCreationTime, ftLastAccessTime, ftLastWriteTime;
        public uint dwVolumeSerialNumber, nFileSizeHigh, nFileSizeLow, nNumberOfLinks, nFileIndexHigh, nFileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    private struct Win32FindData
    {
        public uint dwFileAttributes;
        public long ftCreationTime, ftLastAccessTime, ftLastWriteTime;
        public uint nFileSizeHigh, nFileSizeLow, dwReserved0, dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation info);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, out FileIdInfo info, uint size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstFileExW(string name, int infoLevel, out Win32FindData data, int searchOp, IntPtr filter, int flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool FindNextFileW(IntPtr handle, out Win32FindData data);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr handle);
}
