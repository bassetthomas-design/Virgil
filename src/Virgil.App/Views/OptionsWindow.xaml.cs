using System.Windows;
using Virgil.App.ViewModels;

namespace Virgil.App.Views;

public partial class OptionsWindow : Window
{
    private readonly OptionsViewModel _vm = new();
    public OptionsWindow(){ InitializeComponent(); DataContext=_vm; Loaded+=async(_,__)=> await _vm.LoadAsync(); }
    private async void OnSave(object sender, RoutedEventArgs e){ await _vm.SaveAsync(); DialogResult=true; Close(); }
}
