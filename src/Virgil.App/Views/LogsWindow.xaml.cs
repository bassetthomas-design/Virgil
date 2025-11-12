using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Virgil.App.Views
{
    public partial class LogsWindow : Window
    {
        public LogsWindow() { InitializeComponent(); LoadLatest(); }

        private string LogsDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");

        private void LoadLatest(){
            try{
                if(!Directory.Exists(LogsDir)){ LogBox.Text = "Aucun log pour l'instant."; return; }
                var files = Directory.GetFiles(LogsDir, "*.log").OrderByDescending(File.GetLastWriteTime).ToArray();
                if(files.Length==0){ LogBox.Text = "Aucun log pour l'instant."; return; }
                var sb = new StringBuilder();
                foreach(var f in files.Take(5)){
                    sb.AppendLine($"==== {Path.GetFileName(f)} ====");
                    var txt = File.ReadAllText(f);
                    if(txt.Length > 20000) txt = txt.Substring(txt.Length-20000);
                    sb.AppendLine(txt);
                    sb.AppendLine();
                }
                LogBox.Text = sb.ToString();
                LogBox.CaretIndex = LogBox.Text.Length;
                LogBox.ScrollToEnd();
            }catch(Exception ex){ LogBox.Text = ex.ToString(); }
        }

        private void OnRefresh(object sender, RoutedEventArgs e) => LoadLatest();
    }
}
