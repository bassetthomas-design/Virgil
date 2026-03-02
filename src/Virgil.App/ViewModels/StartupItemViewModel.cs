using Virgil.Services.Startup;

namespace Virgil.App.ViewModels;

public sealed class StartupItemViewModel : BaseViewModel
{
    private bool _isSelected;

    public StartupItemViewModel(StartupItem item)
    {
        Item = item;
        _isSelected = item.IsSelected;
    }

    public StartupItem Item { get; }
    public string Id => Item.Id;
    public string Name => Item.Name;
    public string Location => Item.Location;
    public string Command => Item.Command;
    public string Type => Item.Type;
    public bool IsEssential => Item.IsEssential;
    public bool IsRecommended => Item.IsRecommended;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public StartupItem ToModel() => Item with { IsSelected = IsSelected };
}
