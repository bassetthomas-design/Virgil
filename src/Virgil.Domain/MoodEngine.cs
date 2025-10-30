using Virgil.Core;
namespace Virgil.Domain;
public static class MoodEngine {
  public static Mood FromStats(SystemStats s){
    if (s.CpuTemp>=80 || s.GpuTemp>=80) return Mood.Alert;
    if (s.Cpu>=80 || s.Ram>=90) return Mood.Warn;
    if (s.Cpu<=5 && s.Ram<=20) return Mood.Sleepy;
    return Mood.Happy;
  }
}
