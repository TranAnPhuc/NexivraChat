using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NexivraChatBackend.Repositories;
using System;
using System.IO;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NexivraChatBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly MessageRepository _messageRepository;
        private readonly PrivateChatRepository _privateChatRepository;

        public FilesController(
            IWebHostEnvironment environment,
            MessageRepository messageRepository,
            PrivateChatRepository privateChatRepository)
        {
            _environment = environment;
            _messageRepository = messageRepository;
            _privateChatRepository = privateChatRepository;
        }

        [HttpGet("{year}/{month}/{filename}")]
        public async Task<IActionResult> GetFile(string year, string month, string filename)
        {
            // 1. Sanitize tham số chống path traversal
            if (string.IsNullOrEmpty(year) || string.IsNullOrEmpty(month) || string.IsNullOrEmpty(filename) ||
                !Regex.IsMatch(year, @"^\d{4}$") || !Regex.IsMatch(month, @"^\d{2}$") ||
                !Regex.IsMatch(filename, @"^[A-Za-z0-9._-]+$") || filename.Contains(".."))
            {
                return BadRequest("Tham số đường dẫn không hợp lệ.");
            }

            // 2. Tra cứu message theo attachment_url
            var attachmentUrl = $"/api/files/{year}/{month}/{filename}";
            var msg = await _messageRepository.GetByAttachmentUrl(attachmentUrl);
            if (msg == null)
            {
                return NotFound("Không tìm thấy thông tin tin nhắn đính kèm.");
            }

            // 3. Phân quyền truy cập DM (verify participant)
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            if (msg.PrivateChatId.HasValue)
            {
                var chat = await _privateChatRepository.GetById(msg.PrivateChatId.Value);
                if (chat == null || (chat.User1Id != currentUserId && chat.User2Id != currentUserId))
                {
                    return Forbid();
                }
            }

            // 4. Phục vụ file vật lý kèm headers bảo mật & map MIME chuẩn
            var filePath = Path.Combine(_environment.ContentRootPath, "uploads", year, month, filename);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File vật lý không tồn tại trên máy chủ.");
            }

            var ext = Path.GetExtension(filename).ToLowerInvariant();
            var contentType = ext switch
            {
                ".webp" => "image/webp",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{msg.AttachmentName ?? filename}\"");
            }

            return PhysicalFile(filePath, contentType);
        }
    }
}
