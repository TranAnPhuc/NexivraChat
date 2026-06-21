using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NexivraChatBackend.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string SenderName { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}