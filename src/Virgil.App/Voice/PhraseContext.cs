namespace Virgil.App.Voice;

public record PhraseContext(
    string Action = "", string Mood = "", string Activity = "",
    double Cpu = 0, double Ram = 0, double Temp = 0,
    int Files = 0, long Bytes = 0, int Percent = 0,
    string Error = "",
    bool IsWeekend = false, int Hour = 0
);
