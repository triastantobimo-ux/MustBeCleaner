using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Spike.Shell;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddSingleton<MainViewModel>().AddSingleton<MainWindow>())
            .Build();
        var w = _host.Services.GetRequiredService<MainWindow>();
        if (e.Args.Contains("--smoke"))
            w.Loaded += async (_, _) => { await SmokeRunner.RunAsync(w); Shutdown(); };
        w.Show();
        base.OnStartup(e);
    }
}

