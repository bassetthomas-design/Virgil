using System.Threading.Tasks;
using System.Windows;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private void ProgressIndeterminate()
        {
            try
            {
                ActionProgress.Visibility = Visibility.Visible;
                ActionProgress.IsIndeterminate = true;
            }
            catch { }
        }

        private void ProgressDone()
        {
            try
            {
                ActionProgress.IsIndeterminate = false;
                ActionProgress.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private void ProgressReset()
        {
            try
            {
                ActionProgress.IsIndeterminate = false;
                ActionProgress.Value = 0;
                ActionProgress.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private async Task<string> CleanTempWithProgressInternal()
        {
            ProgressIndeterminate();
            try
            {
                var result = await _cleaning.CleanTempAsync();
                return result;
            }
            finally
            {
                ProgressDone();
            }
        }
    }
}
