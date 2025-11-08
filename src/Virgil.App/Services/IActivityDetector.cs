namespace Virgil.App.Services;

public enum ActivityKind { Idle, Web, Game, Work }

public interface IActivityDetector
{
    ActivityKind Detect();
}
