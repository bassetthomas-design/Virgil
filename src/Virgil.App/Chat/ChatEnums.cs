namespace Virgil.App.Chat
{
    /// <summary>
    /// Kind of chat pipeline / semantic category currently active.
    /// Minimal reconstruction used by the dev branch to satisfy existing
    /// stubs and view models.
    /// </summary>
    public enum ChatKind
    {
        Default = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Success = 4,
    }

    /// <summary>
    /// Type of message in the conversation (user, assistant, system, info, etc.).
    /// The exact semantics can be refined later; only the presence of the
    /// enum values is required right now for the legacy code paths to compile.
    /// </summary>
    public enum MessageType
    {
        User = 0,
        Assistant = 1,
        System = 2,
        Info = 3,
        Warning = 4,
        Error = 5,
        Success = 6,
    }
}
