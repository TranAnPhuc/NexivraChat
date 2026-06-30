using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NexivraChatBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ModerationController : ControllerBase
    {
        private readonly ModerationRepository _moderationRepository;
        private readonly UserRepository _userRepository;
        private readonly ModerationService _moderationService;

        public ModerationController(
            ModerationRepository moderationRepository,
            UserRepository userRepository,
            ModerationService moderationService)
        {
            _moderationRepository = moderationRepository;
            _userRepository = userRepository;
            _moderationService = moderationService;
        }

        private async Task<bool> IsCurrentUserAdmin()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return false;
            }

            var user = await _userRepository.GetById(userId);
            return user != null && user.IsAdmin;
        }

        [HttpGet("words")]
        public async Task<IActionResult> GetBannedWords()
        {
            if (!await IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var words = await _moderationRepository.GetAllBannedWordsAsync();
            return Ok(words);
        }

        [HttpPost("words")]
        public async Task<IActionResult> AddBannedWord([FromBody] AddBannedWordDto dto)
        {
            if (!await IsCurrentUserAdmin())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(dto.Word) || string.IsNullOrWhiteSpace(dto.Tier))
            {
                return BadRequest("Từ cấm và phân tầng không được để trống.");
            }

            var normalized = ModerationService.Normalize(dto.Word);
            if (string.IsNullOrEmpty(normalized))
            {
                return BadRequest("Từ cấm sau khi chuẩn hóa bị rỗng.");
            }

            var existing = await _moderationRepository.GetBannedWordByWordAsync(normalized);
            if (existing != null)
            {
                return BadRequest("Từ cấm này đã tồn tại.");
            }

            var bannedWord = new BannedWord
            {
                Word = normalized,
                Tier = dto.Tier.ToLowerInvariant()
            };

            await _moderationRepository.AddBannedWordAsync(bannedWord);
            await _moderationService.ReloadAsync();

            return Ok(bannedWord);
        }

        [HttpDelete("words/{id}")]
        public async Task<IActionResult> DeleteBannedWord(int id)
        {
            if (!await IsCurrentUserAdmin())
            {
                return Forbid();
            }

            await _moderationRepository.RemoveBannedWordAsync(id);
            await _moderationService.ReloadAsync();

            return Ok();
        }

        [HttpGet("logs")]
        public async Task<IActionResult> GetModerationLogs([FromQuery] int limit = 30, [FromQuery] int offset = 0)
        {
            if (!await IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var logs = await _moderationRepository.GetLogsAsync(limit, offset);
            return Ok(logs);
        }
    }

    public class AddBannedWordDto
    {
        public string Word { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty; // "mask" | "suspect"
    }
}
