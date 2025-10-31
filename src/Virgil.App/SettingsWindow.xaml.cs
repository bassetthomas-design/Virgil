using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
// using System.Text.Json; // décommente si tu sérialises un ViewModel

namespace Virgil.App
{
    public partial class SettingsWindow : Window
    {
        // Exemple : %AppData%\Virgil\config.json
        private static readonly string AppDataRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil");
        private static readonly string ConfigPath = Path.Combine(AppDataRoot, "config.json");

        public SettingsWindow()
        {
            InitializeComponent();

            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    // TODO: désérialiser vers un ViewModel si tu en as un
                    // DataContext = JsonSerializer.Deserialize<SettingsViewModel>(json);
                }
                else
                {
                    Directory.CreateDirectory(AppDataRoot);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsWindow] Load config error: {ex}");
                // Pas de MessageBox ici pour éviter un spam si lancé au démarrage
            }
        }

        // Bouton "Ouvrir configuration"
        private void OpenConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppDataRoot);
                if (!File.Exists(ConfigPath))
                {
                    File.WriteAllText(
                        ConfigPath,
                        "{\n  \"SurveillanceIntervalMs\": 1500,\n  \"Theme\": \"Dark\"\n}\n"
                    );
                }

                // Ouvre l’explorateur en sélectionnant le fichier
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{ConfigPath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Impossible d’ouvrir le dossier de configuration.\n\n" + ex.Message,
                    "Virgil — Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // Bouton "Enregistrer"
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppDataRoot);

                // TODO: si tu as un ViewModel (DataContext), sérialise-le ici :
                // var vm = (SettingsViewModel)DataContext;
                // var json = JsonSerializer.Serialize(vm, new JsonSerializerOptions { WriteIndented = true });

                // Contenu minimal provisoire
                var json =
                    "{\n" +
                    "  \"SurveillanceIntervalMs\": 1500,\n" +
                    "  \"CpuWarnTemp\": 75,\n" +
                    "  \"CpuAlertTemp\": 85,\n" +
                    "  \"Theme\": \"Dark\"\n" +
                    "}\n";

                File.WriteAllText(ConfigPath, json);

                System.Windows.MessageBox.Show(
                    this,
                    "Paramètres enregistrés avec succès.",
                    "Virgil — Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
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
