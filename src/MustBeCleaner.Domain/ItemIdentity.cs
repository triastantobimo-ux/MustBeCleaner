namespace MustBeCleaner.Domain;

/// <summary>
/// Identity of an Item: volume ID, file ID, canonical path, size, modified time, attributes (R7.6).
/// Two identities name the same object when volume ID and file ID match, whatever the path
/// (hard links, junction aliases). Immutable.
/// </summary>
public sealed record ItemIdentity
{
    /// <param name="volumeId">Volume identifier (serial). Not empty.</param>
    /// <param name="fileId">File identifier on that volume. Not empty.</param>
    /// <param name="canonicalPath">Path already normalized by the Policy Core. Not empty.</param>
    /// <param name="size">Logical size in bytes, or null when unavailable. Never coerced to zero (R3.5).</param>
    /// <param name="modifiedUtc">Last write time, UTC.</param>
    /// <param name="attributes">File attributes at observation time.</param>
    public ItemIdentity(
        string volumeId,
        string fileId,
        string canonicalPath,
        long? size,
        DateTime modifiedUtc,
        FileAttributes attributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size), "Size cannot be negative.");

        VolumeId = volumeId;
        FileId = fileId;
        CanonicalPath = canonicalPath;
        Size = size;
        ModifiedUtc = modifiedUtc;
        Attributes = attributes;
    }

    public string VolumeId { get; }
    public string FileId { get; }
    public string CanonicalPath { get; }
    public long? Size { get; }
    public DateTime ModifiedUtc { get; }
    public FileAttributes Attributes { get; }

    /// <summary>True when both identities name the same filesystem object (volume ID and file ID).</summary>
    public bool IsSameObjectAs(ItemIdentity other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return VolumeId == other.VolumeId && FileId == other.FileId;
    }
}
