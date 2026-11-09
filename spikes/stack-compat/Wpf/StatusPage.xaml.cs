using System.Windows;
using System.Windows.Controls;

namespace Spike.Shell;

public partial class StatusPage : Page
{
    public StatusPage() => InitializeComponent();
    private void OnToggle(object sender, RoutedEventArgs e) => ((MainWindow)Window.GetWindow(this)!).ToggleTheme();
}

