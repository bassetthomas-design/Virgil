using Virgil.App.ViewModels;

namespace Virgil.App;

public partial class MainViewModel
{
    public MonitoringViewModel Monitoring { get; }

    public MainViewModel(MonitoringViewModel monitoring)
    {
        Monitoring = monitoring;
    }
}
