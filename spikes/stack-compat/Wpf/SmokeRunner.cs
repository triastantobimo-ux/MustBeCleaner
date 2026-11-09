using System.IO;
using Spike.Core;
using Wpf.Ui.Appearance;

namespace Spike.Shell;

/// <summary>Headless-ish smoke: exercises theme switch, Core P/Invoke and SQLite inside the WPF process, writes a result file.</summary>
internal static class SmokeRunner
{
    public static async Task RunAsync(MainWindow w)
    {
        var dir = Directory.CreateTempSubdirectory("mbc-spike-wpf-").FullName;
        var lines = new List<string>();
        try
        {
            await Task.Delay(300);
            var t1 = w.ToggleTheme(); await Task.Delay(100);
            var t2 = w.ToggleTheme();
            lines.Add($"theme_switch={t1}->{t2}");
            lines.Add($"mica_active={w.MicaActive} os_build={Environment.OSVersion.Version.Build}");
            lines.Add($"nav_items={w.FindName("Nav") is Wpf.Ui.Controls.NavigationView n && n.MenuItems.Count == 2}");
            var f = Path.Combine(dir, "x.txt"); File.WriteAllText(f, "x");
            lines.Add($"file_identity={FileIdentity.Of(f)}");
            using (var s = JournalStore.Open(Path.Combine(dir, "j.db"))) { s.Record("i", "Prepared"); lines.Add($"wal={s.JournalMode} stage={s.StageOf("i")}"); }
            lines.Add("RESULT=PASS");
        }
        catch (Exception ex) { lines.Add("RESULT=FAIL " + ex); }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(dir, true);
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "mbc-spike-smoke.txt"), lines);
    }
}

