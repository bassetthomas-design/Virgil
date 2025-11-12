using Virgil.App.Core;

namespace Virgil.App.ViewModels
{
    public partial class MonitoringViewModel
    {
        public MonitoringViewModel()
        {
            // temporarily avoid referencing missing Mood.Focused
            CurrentMood = default(Mood);
        }

        public Mood CurrentMood { get; set; }
    }
}
