using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Spike.Shell;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        ApplyBackdropOrSolidFallback();
        Loaded += (_, _) => Nav.Navigate(typeof(StatusPage));
    }

    /// <summary>Mica needs Windows 11 build 22000+; otherwise use a solid pastel background.</summary>
    public bool MicaActive { get; private set; }

    private void ApplyBackdropOrSolidFallback()
    {
        MicaActive = Environment.OSVersion.Version.Build >= 22000;
        if (!MicaActive)
        {
            WindowBackdropType = WindowBackdropType.None;
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEE, 0xF3, 0xEC));
        }
    }

    public ApplicationTheme ToggleTheme()
    {
        var next = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(next, MicaActive ? WindowBackdropType.Mica : WindowBackdropType.None);
        return next;
    }
}



