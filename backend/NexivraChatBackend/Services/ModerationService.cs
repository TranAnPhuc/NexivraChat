using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;

namespace NexivraChatBackend.Services
{
    public class ModerationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;
        private readonly ILogger<ModerationService> _logger;
        private readonly AiService _aiService;

        private class CompiledWord
        {
            public string Word { get; set; } = string.Empty;
            public Regex Regex { get; set; } = null!;
        }

        private List<CompiledWord> _maskRegexes = new List<CompiledWord>();
        private List<CompiledWord> _suspectRegexes = new List<CompiledWord>();

        public ModerationService(
            IServiceProvider serviceProvider,
            IConfiguration config,
            ILogger<ModerationService> logger,
            AiService aiService)
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _logger = logger;
            _aiService = aiService;

            // Load wordlist đồng bộ lúc startup
            ReloadAsync().GetAwaiter().GetResult();
        }

        public async Task ReloadAsync()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<ModerationRepository>();
                    var allWords = await repo.GetAllBannedWordsAsync();

                    var maskList = new List<CompiledWord>();
                    var suspectList = new List<CompiledWord>();

                    foreach (var bw in allWords)
                    {
                        var cleanWord = GetSemiNormalized(bw.Word).Replace(" ", "").Replace("_", "").Replace("-", "");
                        if (string.IsNullOrEmpty(cleanWord)) continue;

                        var pattern = string.Join(@"[\s\-_]*", cleanWord.Select(c => Regex.Escape(c.ToString()) + "+"));
                        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

                        var cw = new CompiledWord { Word = bw.Word, Regex = regex };

                        if (bw.Tier.Equals("mask", StringComparison.OrdinalIgnoreCase))
                        {
                            maskList.Add(cw);
                        }
                        else if (bw.Tier.Equals("suspect", StringComparison.OrdinalIgnoreCase))
                        {
                            suspectList.Add(cw);
                        }
                    }

                    _maskRegexes = maskList;
                    _suspectRegexes = suspectList;
                    _logger.LogInformation("Loaded wordlists: {MaskCount} mask words, {SuspectCount} suspect words.", maskList.Count, suspectList.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải danh sách từ cấm từ Database.");
            }
        }

        public static char RemoveDiacriticChar(char c)
        {
            c = char.ToLowerInvariant(c);
            switch (c)
            {
                case 'á': case 'à': case 'ả': case 'ã': case 'ạ':
                case 'â': case 'ấ': case 'ầ': case 'ẩ': case 'ẫ': case 'ậ':
                case 'ă': case 'ắ': case 'ằ': case 'ẳ': case 'ẵ': case 'ặ':
                    return 'a';
                case 'é': case 'è': case 'ẻ': case 'ẽ': case 'ẹ':
                case 'ê': case 'ế': case 'ề': case 'ể': case 'ễ': case 'ệ':
                    return 'e';
                case 'í': case 'ì': case 'ỉ': case 'ĩ': case 'ị':
                    return 'i';
                case 'ó': case 'ò': case 'ỏ': case 'õ': case 'ọ':
                case 'ô': case 'ố': case 'ồ': case 'ổ': case 'ỗ': case 'ộ':
                case 'ơ': case 'ớ': case 'ờ': case 'ở': case 'ỡ': case 'ợ':
                    return 'o';
                case 'ú': case 'ù': case 'ủ': case 'ũ': case 'ụ':
                case 'ư': case 'ứ': case 'ừ': case 'ử': case 'ữ': case 'ự':
                    return 'u';
                case 'ý': case 'ỳ': case 'ỷ': case 'ỹ': case 'ỵ':
                    return 'y';
                case 'đ':
                    return 'd';
                default:
                    return c;
            }
        }

        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.ToLowerInvariant();

            // Map diacritics & leetspeak
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                var unaccented = RemoveDiacriticChar(c);
                if (unaccented == '0') sb.Append('o');
                else if (unaccented == '1' || unaccented == '!') sb.Append('i');
                else if (unaccented == '3') sb.Append('e');
                else if (unaccented == '@') sb.Append('a');
                else if (unaccented == '$') sb.Append('s');
                else sb.Append(unaccented);
            }
            var mapped = sb.ToString();

            // Loại bỏ tất cả ký tự không phải chữ hoặc số (bao gồm khoảng trắng)
            var noSpaces = new StringBuilder();
            foreach (var c in mapped)
            {
                if (char.IsLetterOrDigit(c))
                {
                    noSpaces.Append(c);
                }
            }
            var noSpacesStr = noSpaces.ToString();

            // Gộp các ký tự lặp lại
            var collapsed = new StringBuilder();
            char last = '\0';
            foreach (var c in noSpacesStr)
            {
                if (c != last)
                {
                    collapsed.Append(c);
                    last = c;
                }
            }

            return collapsed.ToString();
        }

        public static string GetSemiNormalized(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                var unaccented = RemoveDiacriticChar(c);
                if (unaccented == '0') sb.Append('o');
                else if (unaccented == '1' || unaccented == '!') sb.Append('i');
                else if (unaccented == '3') sb.Append('e');
                else if (unaccented == '@') sb.Append('a');
                else if (unaccented == '$') sb.Append('s');
                else sb.Append(unaccented);
            }
            return sb.ToString();
        }

        public string ApplyMask(string original, IEnumerable<string> matchedWords)
        {
            if (string.IsNullOrEmpty(original) || matchedWords == null || !matchedWords.Any())
                return original;

            var chars = original.ToCharArray();
            var semiNormalized = GetSemiNormalized(original);

            foreach (var word in matchedWords)
            {
                var cleanWord = GetSemiNormalized(word).Replace(" ", "").Replace("_", "").Replace("-", "");
                var pattern = string.Join(@"[\s\-_]*", cleanWord.Select(c => Regex.Escape(c.ToString()) + "+"));
                var matches = Regex.Matches(semiNormalized, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    for (int i = 0; i < match.Length; i++)
                    {
                        var idx = match.Index + i;
                        if (idx < chars.Length)
                        {
                            chars[idx] = '*';
                        }
                    }
                }
            }

            var resultBuilder = new StringBuilder();
            bool inStars = false;
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '*')
                {
                    if (!inStars)
                    {
                        resultBuilder.Append("***");
                        inStars = true;
                    }
                }
                else
                {
                    resultBuilder.Append(chars[i]);
                    inStars = false;
                }
            }
            return resultBuilder.ToString();
        }

        public async Task<ModerationResult> CheckAsync(string text, string contextType, int? userId = null, string? username = null)
        {
            var result = new ModerationResult { Action = "allow" };

            // Kill-switch check
            var enabledVal = _config["Moderation:Enabled"];
            // Mặc định BẬT; env sai định dạng (vd "1"/"yes") KHÔNG được làm vỡ mọi tin nhắn.
            var enabled = string.IsNullOrEmpty(enabledVal) || !bool.TryParse(enabledVal, out var parsed) || parsed;
            if (!enabled)
            {
                return result;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var semiNormalized = GetSemiNormalized(text);

            // Tìm kiếm các từ bị dính bằng Regex
            var hitMaskWords = _maskRegexes.Where(r => r.Regex.IsMatch(semiNormalized)).Select(r => r.Word).ToList();
            var hitSuspectWords = _suspectRegexes.Where(r => r.Regex.IsMatch(semiNormalized)).Select(r => r.Word).ToList();

            bool hasMask = hitMaskWords.Any();
            bool hasSuspect = hitSuspectWords.Any();

            if (hasSuspect)
            {
                result.Tier = "suspect";
                var aiVerdict = await _aiService.ClassifyAsync(text);
                
                if (aiVerdict == AiModerationVerdict.Toxic)
                {
                    result.Action = "block";
                    result.AiVerdict = "toxic";
                    result.Reason = "Tin nhắn vi phạm chuẩn mực cộng đồng.";
                }
                else if (aiVerdict == AiModerationVerdict.Clean)
                {
                    result.AiVerdict = "clean";
                    if (hasMask)
                    {
                        result.Action = "mask";
                        result.MaskedText = ApplyMask(text, hitMaskWords);
                    }
                    else
                    {
                        result.Action = "allow";
                    }
                }
                else // AiModerationVerdict.Unavailable (Fail-closed!)
                {
                    result.Action = "block";
                    result.AiVerdict = "unavailable";
                    result.Reason = "Hệ thống kiểm duyệt tạm bận, vui lòng thử lại.";
                }
            }
            else if (hasMask)
            {
                result.Tier = "mask";
                result.Action = "mask";
                result.MaskedText = ApplyMask(text, hitMaskWords);
            }

            // Ghi log kiểm duyệt (mọi quyết định khác Allow, HOẶC Allow sau khi gọi AI)
            if (result.Action != "allow" || result.AiVerdict != null)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<ModerationRepository>();
                        var log = new ModerationLog
                        {
                            UserId = userId,
                            Username = username ?? "Ẩn danh",
                            ContextType = contextType,
                            OriginalText = text,
                            Tier = result.Tier ?? (hasMask ? "mask" : "suspect"),
                            AiVerdict = result.AiVerdict,
                            Action = result.Action
                        };
                        await repo.LogAsync(log);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi ghi moderation log vào Database.");
                }
            }

            return result;
        }
    }
}
