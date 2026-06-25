using System;

namespace NexivraChatBackend.Models
{
    public class UserProfile
    {
        public int UserId { get; set; }
        public string? Bio { get; set; }
        public string NativeLanguage { get; set; } = "Vietnamese";
        public string? AiAnalysisJson { get; set; } // Stores the JSON results from Gemini analysis
        public DateTime? LastAnalyzedAt { get; set; }
    }
}
