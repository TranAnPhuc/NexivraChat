using System;

namespace NexivraChatBackend.Models
{
    public class PrivateChat
    {
        public int Id { get; set; }
        public int User1Id { get; set; }
        public int User2Id { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
