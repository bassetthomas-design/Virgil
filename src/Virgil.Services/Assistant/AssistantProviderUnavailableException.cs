using System;

namespace Virgil.Services.Assistant;

public sealed class AssistantProviderUnavailableException : Exception
{
    public AssistantProviderUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
