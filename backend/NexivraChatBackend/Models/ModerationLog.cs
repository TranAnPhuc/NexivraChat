using System;

namespace NexivraChatBackend.Models
{
    public class ModerationLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ContextType { get; set; } = string.Empty; // 'room' | 'private' | 'username'
        public string OriginalText { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty; // 'mask' | 'suspect'
        public string? AiVerdict { get; set; } // 'toxic' | 'clean' | 'unavailable' | null
        public string Action { get; set; } = string.Empty; // 'mask' | 'block' | 'allow'
        public DateTime CreatedAt { get; set; }
    }
}
