namespace Virgil.App.Services;

public class CleanOptions
{
    public bool Thumbnails { get; set; } = true;
    public bool ExplorerCache { get; set; } = true;
    public bool MruRecent { get; set; } = true;
    public bool BrowserCookies { get; set; } = false; // from main
    public bool RestartExplorerAfterRebuild { get; set; } = false; // rambo flag
}
