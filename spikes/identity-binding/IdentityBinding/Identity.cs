using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace IdentityBinding;

/// <summary>Object identity as observed through an open handle.</summary>
public sealed record ObjectIdentity(
    ulong VolumeSerial, ulong FileIdLow, ulong FileIdHigh,
    uint LinkCount, long Size, long LastWriteFileTime, uint Attributes)
{
    public bool IsReparsePoint => (Attributes & Native.ATTR_REPARSE) != 0;
    public bool IsDirectory => (Attributes & Native.ATTR_DIRECTORY) != 0;

    /// <summary>Volume serial + 128-bit file ID. Ignores size/mtime/path.</summary>
    public bool SameObjectAs(ObjectIdentity o) =>
        VolumeSerial == o.VolumeSerial && FileIdLow == o.FileIdLow && FileIdHigh == o.FileIdHigh;

    /// <summary>Same object and unchanged size, mtime and attributes.</summary>
    public bool SameObjectAndMetadata(ObjectIdentity o) =>
        SameObjectAs(o) && Size == o.Size && LastWriteFileTime == o.LastWriteFileTime && Attributes == o.Attributes;
}

public static class Identity
{
    /// <summary>Open without following a final reparse point and without requesting data access.</summary>
    public static SafeFileHandle Open(string path, uint share = Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE | Native.FILE_SHARE_DELETE,
                                      uint access = Native.FILE_READ_ATTRIBUTES)
    {
        var h = Native.CreateFileW(@"\\?\" + path, access, share, IntPtr.Zero, Native.OPEN_EXISTING,
            Native.FILE_FLAG_BACKUP_SEMANTICS | Native.FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero);
        if (h.IsInvalid) throw new IOException("open failed: " + path, new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        return h;
    }

    public static ObjectIdentity Capture(string path)
    {
        using var h = Open(path);
        return Capture(h);
    }

    public static ObjectIdentity Capture(SafeFileHandle h)
    {
        if (!Native.GetFileInformationByHandle(h, out var bh)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        if (!Native.GetFileInformationByHandleEx(h, Native.FileIdInfoClass, out var id, (uint)Marshal.SizeOf<Native.FILE_ID_INFO>()))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return new ObjectIdentity(id.VolumeSerialNumber, id.FileIdLow, id.FileIdHigh, bh.nNumberOfLinks,
            ((long)bh.nFileSizeHigh << 32) | bh.nFileSizeLow, bh.ftLastWriteTime, bh.dwFileAttributes);
    }

    /// <summary>Path the handle currently resolves to (follows renames of the object).</summary>
    public static string FinalPath(SafeFileHandle h)
    {
        var sb = new StringBuilder(1024);
        var n = Native.GetFinalPathNameByHandleW(h, sb, (uint)sb.Capacity, 0);
        if (n == 0 || n > sb.Capacity) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var s = sb.ToString();
        return s.StartsWith(@"\\?\") ? s[4..] : s;
    }

    /// <summary>Reopen the object by volume + 128-bit file ID, using any handle on the same volume as hint.</summary>
    public static SafeFileHandle OpenById(SafeFileHandle volumeHint, ObjectIdentity id,
        uint share = Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE | Native.FILE_SHARE_DELETE)
    {
        var d = new Native.FILE_ID_DESCRIPTOR { dwSize = (uint)Marshal.SizeOf<Native.FILE_ID_DESCRIPTOR>(), Type = 2, IdLow = id.FileIdLow, IdHigh = id.FileIdHigh };
        var h = Native.OpenFileById(volumeHint, ref d, Native.FILE_READ_ATTRIBUTES, share, IntPtr.Zero, Native.FILE_FLAG_BACKUP_SEMANTICS | Native.FILE_FLAG_OPEN_REPARSE_POINT);
        if (h.IsInvalid) throw new IOException("open by id failed", new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        return h;
    }

    /// <summary>All names (volume-relative) of the file's hard links.</summary>
    public static IReadOnlyList<string> HardLinkNames(string path)
    {
        var names = new List<string>();
        uint len = 1024; var sb = new StringBuilder((int)len);
        var h = Native.FindFirstFileNameW(@"\\?\" + path, 0, ref len, sb);
        if (h == (IntPtr)(-1)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            do { names.Add(sb.ToString()); len = 1024; sb.Clear(); }
            while (Native.FindNextFileNameW(h, ref len, sb));
        }
        finally { Native.FindClose(h); }
        return names;
    }
}
