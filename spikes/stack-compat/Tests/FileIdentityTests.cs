using Spike.Core;
using Xunit;

namespace Spike.Tests;

public class FileIdentityTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("mbc-spike-").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Temp_file_has_nonzero_volume_and_file_id_stable_across_reads()
    {
        var p = Path.Combine(_dir, "a.txt");
        File.WriteAllText(p, "x");
        var a = FileIdentity.Of(p);
        var b = FileIdentity.Of(p);
        Assert.NotEqual(0u, a.VolumeSerial);
        Assert.NotEqual(0ul, a.FileIndex);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_files_have_different_identity()
    {
        var p1 = Path.Combine(_dir, "1.txt"); var p2 = Path.Combine(_dir, "2.txt");
        File.WriteAllText(p1, "x"); File.WriteAllText(p2, "x");
        Assert.NotEqual(FileIdentity.Of(p1), FileIdentity.Of(p2));
    }

    [Fact]
    public void Renamed_file_keeps_identity()
    {
        var p1 = Path.Combine(_dir, "1.txt"); var p2 = Path.Combine(_dir, "renamed.txt");
        File.WriteAllText(p1, "x");
        var before = FileIdentity.Of(p1);
        File.Move(p1, p2);
        Assert.Equal(before, FileIdentity.Of(p2));
    }
}
