namespace Virgil.App.Services;

public interface ITaskSchedulerService
{
    bool CreateDailyTask(string name, string exePath, string arguments, int hour, int minute);
    bool DeleteTask(string name);
}
