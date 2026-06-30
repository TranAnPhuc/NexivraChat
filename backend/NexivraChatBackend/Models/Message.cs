using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NexivraChatBackend.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int? RoomId { get; set; }
        public int? PrivateChatId { get; set; }
        public int? SenderId { get; set; }
        public string SenderName { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsAi { get; set; }
        public int? ReplyToId { get; set; }
        public string? ReplyToSenderName { get; set; }
        public string? ReplyToContent { get; set; }
        public DateTime? EditedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentType { get; set; }
        public long? AttachmentSize { get; set; }
        public string? ClientId { get; set; }
    }
}