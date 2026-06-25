using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexivraChatBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly ProfileRepository _profileRepository;
        private readonly UserRepository _userRepository;
        private readonly MessageRepository _messageRepository;
        private readonly AiService _aiService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ProfileController> _logger;

        private const int MaxAvatarBytes = 2 * 1024 * 1024;   // 2 MB
        private const int AvatarSize = 256;                   // px (vuông)
        private const int MaxBioLength = 255;
        private const int MaxSocialLinks = 8;
        private const int MaxLabelLength = 50;
        private const int MaxInterests = 15;
        private const int MaxInterestLength = 30;

        // Mock dùng khi Gemini không khả dụng / trả JSON hỏng (DRY: 1 nguồn duy nhất).
        private const string MockAnalysisJson = @"
                {
                  ""communicationStyle"": ""Bạn có phong cách giao tiếp tự nhiên, thân thiện và năng động. Thích trao đổi trực tiếp và luôn mang năng lượng tích cực vào cuộc đối thoại."",
                  ""keyTraits"": [""Thân thiện"", ""Nhiệt huyết"", ""Hòa đồng""],
                  ""habits"": [
                    ""Thường phản hồi tin nhắn nhanh chóng"",
                    ""Thích chia sẻ ý tưởng mới"",
                    ""Sử dụng ngôn ngữ gần gũi, cởi mở""
                  ],
                  ""radarMetrics"": {
                    ""friendliness"": 85,
                    ""responsiveness"": 90,
                    ""clarity"": 75,
                    ""creativity"": 80,
                    ""professionalism"": 60
                  }
                }";

        public ProfileController(
            ProfileRepository profileRepository,
            UserRepository userRepository,
            MessageRepository messageRepository,
            AiService aiService,
            IWebHostEnvironment env,
            ILogger<ProfileController> logger)
        {
            _profileRepository = profileRepository;
            _userRepository = userRepository;
            _messageRepository = messageRepository;
            _aiService = aiService;
            _env = env;
            _logger = logger;
        }

        // DRY: mọi action trả về cùng một shape. Parse JSONB sang mảng cho frontend.
        private object BuildResponse(UserProfile profile, string username)
        {
            return new
            {
                profile.UserId,
                Username = username,
                profile.Bio,
                profile.NativeLanguage,
                profile.AiAnalysisJson,
                profile.LastAnalyzedAt,
                profile.AvatarUrl,
                SocialLinks = ParseSocialLinks(profile.SocialLinksJson),
                Interests = ParseInterests(profile.InterestsJson)
            };
        }

        private List<SocialLinkDto> ParseSocialLinks(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SocialLinkDto>();
            try
            {
                return JsonSerializer.Deserialize<List<SocialLinkDto>>(json) ?? new List<SocialLinkDto>();
            }
            catch (JsonException)
            {
                _logger.LogWarning("social_links JSON hỏng, trả mảng rỗng. Raw: {Json}", json);
                return new List<SocialLinkDto>();
            }
        }

        private List<string> ParseInterests(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (JsonException)
            {
                _logger.LogWarning("interests JSON hỏng, trả mảng rỗng. Raw: {Json}", json);
                return new List<string>();
            }
        }

        private static bool IsValidHttpUrl(string? url) =>
            !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var u)
            && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

        [HttpGet]
        public async Task<IActionResult> GetOwnProfile()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

            var profile = await _profileRepository.GetByUserId(userId)
                          ?? new UserProfile { UserId = userId, Bio = string.Empty };

            return Ok(BuildResponse(profile, username));
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserProfile(int userId)
        {
            var targetUser = await _userRepository.GetById(userId);
            if (targetUser == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var profile = await _profileRepository.GetByUserId(userId)
                          ?? new UserProfile { UserId = userId, Bio = string.Empty };

            return Ok(BuildResponse(profile, targetUser.Username));
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            // Bio: trim + giới hạn độ dài (thống nhất với DB và frontend).
            var bio = dto.Bio?.Trim();
            if (bio != null && bio.Length > MaxBioLength)
            {
                return BadRequest($"Tiểu sử tối đa {MaxBioLength} ký tự.");
            }

            // Social links: tối đa N, mỗi url phải http/https (chống XSS), label trim.
            var links = dto.SocialLinks ?? new List<SocialLinkDto>();
            if (links.Count > MaxSocialLinks)
            {
                return BadRequest($"Tối đa {MaxSocialLinks} liên kết mạng xã hội.");
            }
            var cleanLinks = new List<SocialLinkDto>();
            foreach (var link in links)
            {
                if (!IsValidHttpUrl(link.Url))
                {
                    return BadRequest($"Liên kết không hợp lệ: \"{link.Url}\". Chỉ chấp nhận http/https.");
                }
                var label = (link.Label ?? string.Empty).Trim();
                if (label.Length > MaxLabelLength) label = label.Substring(0, MaxLabelLength);
                cleanLinks.Add(new SocialLinkDto { Label = label, Url = link.Url!.Trim() });
            }

            // Interests: trim + lowercase + bỏ rỗng + dedupe + giới hạn.
            var rawInterests = dto.Interests ?? new List<string>();
            var cleanInterests = rawInterests
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .Select(t => t.Length > MaxInterestLength ? t.Substring(0, MaxInterestLength) : t)
                .Distinct()
                .Take(MaxInterests)
                .ToList();

            var profile = await _profileRepository.GetByUserId(userId)
                          ?? new UserProfile { UserId = userId };

            profile.Bio = bio;
            if (!string.IsNullOrWhiteSpace(dto.NativeLanguage))
            {
                profile.NativeLanguage = dto.NativeLanguage;
            }
            profile.SocialLinksJson = JsonSerializer.Serialize(cleanLinks);
            profile.InterestsJson = JsonSerializer.Serialize(cleanInterests);

            await _profileRepository.Upsert(profile);

            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
            _logger.LogInformation("Profile updated cho user {UserId}: {LinkCount} link, {TagCount} tag.",
                userId, cleanLinks.Count, cleanInterests.Count);
            return Ok(BuildResponse(profile, username));
        }

        [HttpPost("avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile? file)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }
            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

            if (file == null || file.Length == 0)
            {
                return BadRequest("Chưa chọn ảnh.");
            }
            if (file.Length > MaxAvatarBytes)
            {
                return BadRequest($"Ảnh tối đa {MaxAvatarBytes / (1024 * 1024)} MB.");
            }

            var avatarsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "avatars");
            Directory.CreateDirectory(avatarsDir);

            var fileName = $"{userId}_{Guid.NewGuid():N}.webp";
            var fullPath = Path.Combine(avatarsDir, fileName);

            try
            {
                // ImageSharp decode tự kiểm tra đây là ảnh raster thật (SVG/.exe đổi đuôi sẽ ném ở đây).
                using (var stream = file.OpenReadStream())
                using (var image = await Image.LoadAsync(stream))
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(AvatarSize, AvatarSize),
                        Mode = ResizeMode.Crop   // crop vuông, lấp đầy 256x256
                    }));
                    await image.SaveAsync(fullPath, new WebpEncoder());
                }
            }
            catch (UnknownImageFormatException)
            {
                _logger.LogWarning("User {UserId} upload file không phải ảnh hợp lệ ({Type}).", userId, file.ContentType);
                return BadRequest("File không phải ảnh hợp lệ. Chỉ chấp nhận PNG, JPEG, WEBP.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lưu avatar cho user {UserId}.", userId);
                return StatusCode(500, "Không lưu được ảnh. Vui lòng thử lại.");
            }

            var profile = await _profileRepository.GetByUserId(userId)
                          ?? new UserProfile { UserId = userId };

            // Xoá file cũ (non-fatal nếu thất bại).
            DeleteAvatarFile(profile.AvatarUrl);

            profile.AvatarUrl = $"/avatars/{fileName}";

            try
            {
                await _profileRepository.Upsert(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upsert avatar thất bại cho user {UserId}, dọn file mồ côi.", userId);
                DeleteAvatarFile(profile.AvatarUrl);
                return StatusCode(500, "Không lưu được ảnh. Vui lòng thử lại.");
            }

            _logger.LogInformation("Avatar updated cho user {UserId}: {File} ({Bytes} bytes).", userId, fileName, file.Length);
            return Ok(BuildResponse(profile, username));
        }

        [HttpDelete("avatar")]
        public async Task<IActionResult> DeleteAvatar()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }
            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

            var profile = await _profileRepository.GetByUserId(userId);
            if (profile == null)
            {
                return Ok(BuildResponse(new UserProfile { UserId = userId }, username));
            }

            DeleteAvatarFile(profile.AvatarUrl);
            profile.AvatarUrl = null;
            await _profileRepository.Upsert(profile);

            _logger.LogInformation("Avatar removed cho user {UserId}.", userId);
            return Ok(BuildResponse(profile, username));
        }

        // Xoá file avatar theo url tương đối; bỏ qua an toàn nếu null/không tồn tại.
        private void DeleteAvatarFile(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl)) return;
            try
            {
                var fileName = Path.GetFileName(avatarUrl);
                var avatarsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "avatars");
                var path = Path.Combine(avatarsDir, fileName);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không xoá được file avatar cũ: {Url}", avatarUrl);
            }
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeProfile()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

            // Fetch 30 latest messages sent by this user
            var latestMessages = await _messageRepository.GetLatestMessagesBySender(username, 30);
            
            if (latestMessages.Count < 3)
            {
                return BadRequest("Bạn cần gửi ít nhất 3 tin nhắn trong phòng chat để AI có đủ dữ liệu phân tích tính cách.");
            }

            // Concatenate messages for the prompt
            var messagesText = string.Join("\n", latestMessages.Select(m => $"- {m.Content}"));

            var systemInstruction = "Analyze the communication style of the user based on their recent chat messages. You must return a JSON object with the following fields: 'communicationStyle' (string, summary in Vietnamese), 'keyTraits' (array of strings, 3 key traits in Vietnamese), 'habits' (array of strings, 2-3 communication habits in Vietnamese), and 'radarMetrics' (object containing 'friendliness', 'responsiveness', 'clarity', 'creativity', and 'professionalism' as integers between 10 and 100). Respond ONLY with the raw JSON string, do not wrap it in markdown code block ```json or add any explanation.";

            var prompt = $"Dưới đây là danh sách các tin nhắn gần nhất của người dùng '{username}':\n\n{messagesText}\n\nHãy phân tích phong cách giao tiếp và trả về JSON theo đúng định dạng yêu cầu.";

            var aiResponse = await _aiService.GenerateContentAsync(prompt, systemInstruction);

            // Clean up and validate JSON response
            if (!string.IsNullOrWhiteSpace(aiResponse))
            {
                aiResponse = aiResponse.Trim();
                if (aiResponse.StartsWith("```json"))
                {
                    aiResponse = aiResponse.Substring(7).Trim();
                }
                else if (aiResponse.StartsWith("```"))
                {
                    aiResponse = aiResponse.Substring(3).Trim();
                }

                if (aiResponse.EndsWith("```"))
                {
                    aiResponse = aiResponse.Substring(0, aiResponse.Length - 3).Trim();
                }
            }

            // Fallback mock nếu rỗng/lỗi hoặc thiếu Gemini key.
            if (string.IsNullOrWhiteSpace(aiResponse) || aiResponse.StartsWith("[Lỗi"))
            {
                aiResponse = MockAnalysisJson;
            }
            else
            {
                // Xác thực JSON; nếu hỏng dùng mock.
                try
                {
                    using var doc = JsonDocument.Parse(aiResponse);
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Gemini trả JSON không hợp lệ cho user {UserId}, dùng mock.", userId);
                    aiResponse = MockAnalysisJson;
                }
            }

            var profile = await _profileRepository.GetByUserId(userId)
                          ?? new UserProfile { UserId = userId };
            profile.AiAnalysisJson = aiResponse;
            profile.LastAnalyzedAt = DateTime.UtcNow;

            await _profileRepository.Upsert(profile);

            return Ok(BuildResponse(profile, username));
        }
    }

    public class UpdateProfileDto
    {
        public string? Bio { get; set; }
        public string? NativeLanguage { get; set; }
        public List<SocialLinkDto>? SocialLinks { get; set; }
        public List<string>? Interests { get; set; }
    }

    public class SocialLinkDto
    {
        public string? Label { get; set; }
        public string? Url { get; set; }
    }
}
