using System;
using System.Linq;
using System.Windows;
using Virgil.Core.Config;

namespace Virgil.App
{
    public partial class SettingsWindow : Window
    {
        private VirgilConfig _cfg;
        public SettingsWindow(VirgilConfig cfg)
        {
            InitializeComponent();
            _cfg = cfg;
            LoadUi();
            BtnSave.Click += (_,__) => { SaveUi(); DialogResult = true; Close(); };
            BtnCancel.Click += (_,__) => { DialogResult = false; Close(); };
        }

        private void LoadUi()
        {
            CpuWarnBox.Text   = _cfg.CpuWarn.ToString();
            GpuWarnBox.Text   = _cfg.GpuWarn.ToString();
            RamWarnBox.Text   = _cfg.RamWarn.ToString();
            TempWarnBox.Text  = _cfg.TempWarn.ToString();
            TempAlertBox.Text = _cfg.TempAlert.ToString();
            PunchFreqBox.Text = _cfg.PunchlineFrequency.ToString();
            MoodFreqBox.Text  = _cfg.MoodFrequency.ToString();

            ToneBox.SelectedItem  = ToneBox.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                                        .FirstOrDefault(i => (string)i.Content == _cfg.Tone);
            ThemeBox.SelectedItem = ThemeBox.Items.Cast<System.Windows.Controls.ComboBoxItem>()
                                        .FirstOrDefault(i => (string)i.Content == _cfg.Theme);
        }

        private void SaveUi()
        {
            int.TryParse(CpuWarnBox.Text, out _cfg.CpuWarn);
            int.TryParse(GpuWarnBox.Text, out _cfg.GpuWarn);
            int.TryParse(RamWarnBox.Text, out _cfg.RamWarn);
            int.TryParse(TempWarnBox.Text, out _cfg.TempWarn);
            int.TryParse(TempAlertBox.Text, out _cfg.TempAlert);
            int.TryParse(PunchFreqBox.Text, out _cfg.PunchlineFrequency);
            int.TryParse(MoodFreqBox.Text, out _cfg.MoodFrequency);

            _cfg.Tone  = ((System.Windows.Controls.ComboBoxItem)ToneBox.SelectedItem).Content?.ToString() ?? "humor";
            _cfg.Theme = ((System.Windows.Controls.ComboBoxItem)ThemeBox.SelectedItem).Content?.ToString() ?? "dark";

            _cfg.Save(); // vers %AppData%\Virgil\config\settings.json
        }
    }
}
