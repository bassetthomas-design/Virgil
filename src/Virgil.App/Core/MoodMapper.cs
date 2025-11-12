using System;

namespace Virgil.App.Core
{
    public enum Mood { Idle, Happy, Busy, Angry, Sleepy }

    public class MoodMapper
    {
        public event Action<Mood>? MoodChanged;

        public void Set(Mood mood)
        {
            MoodChanged?.Invoke(mood);
        }
    }
}
