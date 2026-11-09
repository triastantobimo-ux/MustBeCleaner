namespace MustBeCleaner.Inspector;

/// <summary>What kind of reparse point an entry is. Every kind other than <see cref="None"/> is never traversed (R7.1, R7.2).</summary>
public enum ReparseKind
{
    /// <summary>Not a reparse point.</summary>
    None,
    /// <summary>Symbolic link.</summary>
    SymbolicLink,
    /// <summary>Directory junction or volume mount point (same tag).</summary>
    JunctionOrMountPoint,
    /// <summary>Cloud files placeholder (OneDrive and similar).</summary>
    Cloud,
    /// <summary>Any other tag. Treated as untraversable until classified (R7.2).</summary>
    Unclassified,
}

/// <summary>Maps a Win32 reparse tag to a <see cref="ReparseKind"/> (R7.1, R7.2).</summary>
public static class ReparseClassifier
{
    private const uint MountPoint = 0xA0000003;
    private const uint Symlink = 0xA000000C;
    private const uint CloudBase = 0x9000001A;
    private const uint CloudMask = 0xFFFF0FFF; // the four bits 8..11 carry the cloud provider state

    /// <summary>Classifies the tag of an entry whose reparse attribute is set. Unknown tags are <see cref="ReparseKind.Unclassified"/>.</summary>
    public static ReparseKind Classify(uint tag) => tag switch
    {
        MountPoint => ReparseKind.JunctionOrMountPoint,
        Symlink => ReparseKind.SymbolicLink,
        _ when (tag & CloudMask) == CloudBase => ReparseKind.Cloud,
        _ => ReparseKind.Unclassified,
    };
}
