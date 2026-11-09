namespace MustBeCleaner.Domain.Tests;

public class ItemIdentityTests
{
    private static ItemIdentity Make(
        string volumeId = "vol-1",
        string fileId = "file-1",
        string path = @"C:\Users\x\AppData\Local\Foo\cache",
        long? size = 10) =>
        new(volumeId, fileId, path, size, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), FileAttributes.Directory);

    // R7.6: identity carries volume ID, file ID, canonical path, size, mtime, attributes.
    [Fact]
    public void Exposes_all_identity_fields()
    {
        var id = Make();

        Assert.Equal("vol-1", id.VolumeId);
        Assert.Equal("file-1", id.FileId);
        Assert.Equal(@"C:\Users\x\AppData\Local\Foo\cache", id.CanonicalPath);
        Assert.Equal(10, id.Size);
        Assert.Equal(FileAttributes.Directory, id.Attributes);
    }

    // R3.5: unavailable size is explicit, never zero.
    [Fact]
    public void Unavailable_size_is_null_not_zero()
    {
        Assert.Null(Make(size: null).Size);
    }

    [Theory]
    [InlineData("", "f", @"C:\a")]
    [InlineData("v", " ", @"C:\a")]
    [InlineData("v", "f", "")]
    public void Rejects_missing_identity_parts(string volumeId, string fileId, string path)
    {
        Assert.Throws<ArgumentException>(() => Make(volumeId, fileId, path));
    }

    [Fact]
    public void Rejects_negative_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(size: -1));
    }

    // Glossary "Object identity": identity is volume + file ID, not the path string.
    [Fact]
    public void Same_object_is_decided_by_volume_and_file_id_not_path()
    {
        var a = Make(path: @"C:\a");
        var aliasOnOtherPath = Make(path: @"C:\b");
        var otherFile = Make(fileId: "file-2", path: @"C:\a");
        var otherVolume = Make(volumeId: "vol-2", path: @"C:\a");

        Assert.True(a.IsSameObjectAs(aliasOnOtherPath));
        Assert.False(a.IsSameObjectAs(otherFile));
        Assert.False(a.IsSameObjectAs(otherVolume));
    }
}
