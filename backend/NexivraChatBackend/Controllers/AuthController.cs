using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;

namespace NexivraChatBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserRepository _userRepository;
        private readonly TokenService _tokenService;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthController(UserRepository userRepository, TokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _passwordHasher = new PasswordHasher<User>();
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest("Tên đăng nhập và mật khẩu không được để trống.");
            }

            var existingUser = _userRepository.GetByUsername(dto.Username);
            if (existingUser != null)
            {
                return BadRequest("Tên đăng nhập đã tồn tại.");
            }

            var user = new User
            {
                Username = dto.Username,
                CreatedAt = DateTime.Now
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            _userRepository.Create(user);

            var token = _tokenService.CreateToken(user);
            return Ok(new AuthResponseDto { Username = user.Username, Token = token });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest("Tên đăng nhập và mật khẩu không được để trống.");
            }

            var user = _userRepository.GetByUsername(dto.Username);
            if (user == null)
            {
                return Unauthorized("Tên đăng nhập hoặc mật khẩu không chính xác.");
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                return Unauthorized("Tên đăng nhập hoặc mật khẩu không chính xác.");
            }

            var token = _tokenService.CreateToken(user);
            return Ok(new AuthResponseDto { Username = user.Username, Token = token });
        }
    }

    public class RegisterDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Username { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
