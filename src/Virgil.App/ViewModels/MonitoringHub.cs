using Virgil.Domain;

namespace Virgil.App.ViewModels
{
    public static class MonitoringHub
    {
        public static readonly MonitoringViewModel Instance = new MonitoringViewModel { CurrentMood = default(Mood) };
    }
}
