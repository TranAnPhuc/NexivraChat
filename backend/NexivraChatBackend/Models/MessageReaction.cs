using System;

namespace NexivraChatBackend.Models
{
    public class MessageReaction
    {
        public int MessageId { get; set; }
        public int UserId { get; set; }
        public string Emoji { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
