using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Virgil.App
{
    public partial class SettingsWindow : Window
    {
        // Exemple de chemin de config : %AppData%\Virgil\config.json
        private static readonly string AppDataRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil");
        private static readonly string ConfigPath = Path.Combine(AppDataRoot, "config.json");

        public SettingsWindow()
        {
            InitializeComponent();
            try
            {
                // Chargement léger des paramètres si le fichier existe (optionnel, extensible)
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    // TODO: désérialiser vers un ViewModel si nécessaire
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
                // Ne pas faire planter la fenêtre si lecture impossible
            }
        }

        // Bouton "Ouvrir configuration" — ouvre le dossier contenant le fichier de config
        private void OpenConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppDataRoot);
                if (!File.Exists(ConfigPath))
                {
                    // Crée un squelette minimal si absent
                    File.WriteAllText(ConfigPath, "{\n  \"SurveillanceIntervalMs\": 1500,\n  \"Theme\": \"Dark\"\n}\n");
                }

                // Sélectionne le fichier dans l’Explorateur
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{ConfigPath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Impossible d’ouvrir le dossier de configuration.\n\n" + ex.Message,
                    "Virgil — Configuration",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Bouton "Enregistrer" — persiste les paramètres actuels
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppDataRoot);

                // TODO: Si tu as un ViewModel (ex: DataContext), sérialise-le ici.
                // var vm = DataContext as SettingsViewModel;
                // var json = JsonSerializer.Serialize(vm, new JsonSerializerOptions { WriteIndented = true });

                // En attendant, on sauvegarde un contenu minimal (à remplacer par tes vraies propriétés)
                var json = "{\n" +
                           "  \"SurveillanceIntervalMs\": 1500,\n" +
                           "  \"CpuWarnTemp\": 75,\n" +
                           "  \"CpuAlertTemp\": 85,\n" +
                           "  \"Theme\": \"Dark\"\n" +
                           "}\n";

                File.WriteAllText(ConfigPath, json);

                MessageBox.Show(this,
                    "Paramètres enregistrés avec succès.",
                    "Virgil — Configuration",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Échec de l’enregistrement des paramètres.\n\n" + ex.Message,
                    "Virgil — Configuration",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
