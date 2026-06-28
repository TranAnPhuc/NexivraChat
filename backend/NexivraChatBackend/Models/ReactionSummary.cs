namespace NexivraChatBackend.Models
{
    public class ReactionSummary
    {
        public int MessageId { get; set; }
        public string Emoji { get; set; }
        public int Count { get; set; }
        public bool MineReacted { get; set; }
    }
}
