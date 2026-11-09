using CommunityToolkit.Mvvm.ComponentModel;

namespace Spike.Shell;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _status = "Spike ready";
}

