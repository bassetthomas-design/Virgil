using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Virgil.App.ViewModels;
using Virgil.Services.Startup;

namespace Virgil.App.Views;

public partial class StartupOptimizationWindow : Window
{
    public ObservableCollection<StartupItemViewModel> Items { get; }

    public StartupOptimizationWindow(IEnumerable<StartupItem> items)
    {
        InitializeComponent();
        Items = new ObservableCollection<StartupItemViewModel>(items.Select(i => new StartupItemViewModel(i)));
        DataContext = this;
    }

    public IReadOnlyList<StartupItem> SelectedItems => Items
        .Where(item => item.IsSelected && !item.IsEssential)
        .Select(item => item.ToModel())
        .ToList();

    private void SelectRecommended_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
        {
            if (!item.IsEssential)
            {
                item.IsSelected = item.IsRecommended;
            }
        }
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
        {
            if (!item.IsEssential)
            {
                item.IsSelected = false;
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
