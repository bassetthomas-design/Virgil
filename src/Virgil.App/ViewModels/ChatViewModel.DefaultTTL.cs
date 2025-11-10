using System;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel
    {
        // Default TTL for non-pinned messages when caller doesn't specify ttlMs
        public int DefaultTtlMs { get; set; } = 60000;
    }
}
