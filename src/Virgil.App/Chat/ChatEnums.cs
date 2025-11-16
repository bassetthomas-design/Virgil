namespace Virgil.App.Chat
{
    /// <summary>
    /// Kind of chat pipeline currently active. This is a minimal reconstruction
    /// used by the dev branch to satisfy existing stubs and view models.
    /// </summary>
    public enum ChatKind
    {
        Default = 0,
    }

    /// <summary>
    /// Type of message in the conversation (user, assistant, system, etc.).
    /// The exact semantics can be refined later; only the presence of the
    /// enum is required right now for the legacy code paths to compile.
    /// </summary>
    public enum MessageType
    {
        User = 0,
        Assistant = 1,
        System = 2,
    }
}
