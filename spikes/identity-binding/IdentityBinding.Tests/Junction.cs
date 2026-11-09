using System.Diagnostics;
namespace IdentityBinding.Tests;

public static class Junction
{
    /// <summary>Create a junction via mklink /J. Both paths must be inside the sandbox.</summary>
    public static void Create(string link, string target)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException(p.StandardError.ReadToEnd());
    }
}
