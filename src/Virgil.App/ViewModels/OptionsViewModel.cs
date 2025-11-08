using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Virgil.App.Services;

namespace Virgil.App.ViewModels;

public class OptionsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings = new JsonSettingsService();

    private bool _thumbnails = true;
    private bool _explorerCache = true;
    private bool _mruRecent = true;
    private bool _browserCookies = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Thumbnails { get => _thumbnails; set { _thumbnails = value; OnChanged(); } }
    public bool ExplorerCache { get => _explorerCache; set { _explorerCache = value; OnChanged(); } }
    public bool MruRecent { get => _mruRecent; set { _mruRecent = value; OnChanged(); } }
    public bool BrowserCookies { get => _browserCookies; set { _browserCookies = value; OnChanged(); } }

    public async Task LoadAsync()
    {
        var o = await _settings.LoadAsync();
        Thumbnails = o.Thumbnails; ExplorerCache = o.ExplorerCache; MruRecent = o.MruRecent; BrowserCookies = o.BrowserCookies;
    }

    public Task SaveAsync() => _settings.SaveAsync(new CleanOptions{ Thumbnails = Thumbnails, ExplorerCache = ExplorerCache, MruRecent = MruRecent, BrowserCookies = BrowserCookies });

    private void OnChanged([CallerMemberName] string? n=null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
