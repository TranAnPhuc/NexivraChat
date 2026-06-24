using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexivraChatBackend.Services;
using System.Threading.Tasks;

namespace NexivraChatBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TranslationController : ControllerBase
    {
        private readonly TranslationService _translationService;

        public TranslationController(TranslationService translationService)
        {
            _translationService = translationService;
        }

        [HttpPost]
        public async Task<IActionResult> Translate([FromBody] TranslationRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Text))
            {
                return BadRequest("Nội dung dịch không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(dto.TargetLanguage))
            {
                return BadRequest("Ngôn ngữ đích không được để trống.");
            }

            var translatedText = await _translationService.TranslateTextAsync(dto.Text, dto.TargetLanguage);
            return Ok(new { TranslatedText = translatedText });
        }
    }

    public class TranslationRequestDto
    {
        public string Text { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
    }
}
