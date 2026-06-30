using System;

namespace NexivraChatBackend.Models
{
    public class BannedWord
    {
        public int Id { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty; // "mask" | "suspect"
        public DateTime CreatedAt { get; set; }
    }
}
