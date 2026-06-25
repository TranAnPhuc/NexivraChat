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

        // Đường dẫn tương đối tới avatar đã upload, vd "/avatars/3_a1b2.webp" (null = dùng initials).
        public string? AvatarUrl { get; set; }

        // Lưu JSONB dưới dạng chuỗi, giống pattern AiAnalysisJson. Controller (de)serialize sang mảng.
        // SocialLinksJson: mảng [{ "label": "...", "url": "..." }]; InterestsJson: mảng ["...", ...].
        public string? SocialLinksJson { get; set; }
        public string? InterestsJson { get; set; }
    }
}
