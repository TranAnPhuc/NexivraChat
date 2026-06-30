namespace NexivraChatBackend.Models
{
    public class ModerationResult
    {
        public string Action { get; set; } = "allow"; // "allow" | "mask" | "block"
        public string? MaskedText { get; set; }
        public string? Reason { get; set; }
        public string? Tier { get; set; } // "mask" | "suspect" | null
        public string? AiVerdict { get; set; } // "toxic" | "clean" | "unavailable" | null
    }
}
