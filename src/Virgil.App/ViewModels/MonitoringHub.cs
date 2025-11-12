using Virgil.App.Core;

namespace Virgil.App.ViewModels
{
    public static class MonitoringHub
    {
        public static readonly MonitoringViewModel Instance = new MonitoringViewModel { CurrentMood = default(Mood) };
    }
}
