using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Spike.Core;

/// <summary>Volume serial + 64-bit file index of a file (Glossary: Item identity).</summary>
public readonly record struct FileIdentity(uint VolumeSerial, ulong FileIndex)
{
    public static FileIdentity Of(string path)
    {
        using var h = CreateFileW(path, 0, 7, IntPtr.Zero, 3, 0x02000000 /* BACKUP_SEMANTICS */, IntPtr.Zero);
        if (h.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!GetFileInformationByHandle(h, out var i)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new FileIdentity(i.VolumeSerialNumber, ((ulong)i.FileIndexHigh << 32) | i.FileIndexLow);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle h, out BY_HANDLE_FILE_INFORMATION info);
}
