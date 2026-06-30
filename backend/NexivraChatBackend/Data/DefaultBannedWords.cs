using System.Collections.Generic;

namespace NexivraChatBackend.Data
{
    public static class DefaultBannedWords
    {
        public static readonly List<(string Word, string Tier)> Words = new List<(string Word, string Tier)>
        {
            // Mask tier (Che *** tin nhắn)
            ("dm", "mask"),
            ("vl", "mask"),
            ("djt", "mask"),
            ("fuck", "mask"),
            ("shit", "mask"),
            ("bitch", "mask"),
            ("cc", "mask"),
            ("cl", "mask"),

            // Suspect tier (Nghi ngờ, chuyển AI kiểm tra)
            ("chem", "suspect"),
            ("giet", "suspect"),
            ("hack", "suspect"),
            ("bom", "suspect"),
            ("scam", "suspect"),
            ("lua dao", "suspect"),
            ("ban ma tuy", "suspect"),
            ("khung bo", "suspect")
        };
    }
}
