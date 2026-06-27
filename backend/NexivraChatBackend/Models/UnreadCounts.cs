using System.Collections.Generic;

namespace NexivraChatBackend.Models
{
    // Số tin chưa đọc của một user, tách theo phòng và theo chat 1-1 (DM).
    // Key = id phòng / id private_chat; Value = số tin chưa đọc.
    public class UnreadCounts
    {
        public Dictionary<int, int> Rooms { get; set; } = new();
        public Dictionary<int, int> PrivateChats { get; set; } = new();
    }
}
