using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Virgil.App.Services;
using Virgil.App.ViewModels;

namespace Virgil.App
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow()
        {
            InitializeComponent();
            _settingsService = new SettingsService();
            _viewModel = new SettingsViewModel(_settingsService);
            DataContext = _viewModel;
        }

        private void OpenConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Virgil",
                    "settings.json"
                );

                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                if (!File.Exists(settingsPath))
                {
                    _settingsService.Save();
                }

                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{settingsPath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Impossible d’ouvrir le dossier de configuration.\n\n" + ex.Message,
                    "Virgil — Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.Save();
                MessageBox.Show(
                    this,
                    "Paramètres enregistrés avec succès.",
                    "Virgil — Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Échec de l’enregistrement des paramètres.\n\n" + ex.Message,
                    "Virgil — Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}
