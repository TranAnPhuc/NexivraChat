using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NexivraChatBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };
        private static readonly string[] AllowedMimeTypes = {
            "image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf"
        };

        public UploadController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB limit (Threat-Model #1)
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Vui lòng chọn một file đính kèm.");
            }

            // 1. Kiểm tra dung lượng (Threat-Model #1)
            if (file.Length > 10 * 1024 * 1024)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge, "Kích thước file không được vượt quá 10 MB.");
            }

            // 2. Kiểm tra Whitelist loại file & Cấm SVG (Threat-Model #2)
            var originalFileName = Path.GetFileName(file.FileName); // Anti path-traversal
            var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
            var contentType = file.ContentType?.ToLowerInvariant() ?? "";

            if (ext == ".svg" || contentType.Contains("svg"))
            {
                return BadRequest("File SVG chứa rủi ro mã độc (XSS) và bị từ chối tuyệt đối.");
            }

            if (!AllowedExtensions.Contains(ext) || !AllowedMimeTypes.Contains(contentType))
            {
                return BadRequest("Loại file không nằm trong danh sách hỗ trợ (Chỉ hỗ trợ JPG, PNG, GIF, WEBP, PDF).");
            }

            // 3. Đổi tên file an toàn {guid}{ext} (Threat-Model #3)
            var year = DateTime.Now.ToString("yyyy");
            var month = DateTime.Now.ToString("MM");
            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "uploads", year, month);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var serverFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, serverFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativeUrl = $"/api/files/{year}/{month}/{serverFileName}";

            // Sanitize tên gốc chỉ để hiển thị (loại bỏ ký tự lạ)
            var safeDisplayName = Regex.Replace(originalFileName, @"[^\w\-. ]", "_");

            return Ok(new
            {
                url = relativeUrl,
                name = safeDisplayName,
                type = contentType,
                size = file.Length
            });
        }
    }
}
