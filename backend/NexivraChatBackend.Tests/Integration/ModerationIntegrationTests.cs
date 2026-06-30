using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;
using NexivraChatBackend.Tests.Fixtures;
using Dapper;

namespace NexivraChatBackend.Tests.Integration
{
    public class FakeAiService : AiService
    {
        public static AiModerationVerdict Verdict { get; set; } = AiModerationVerdict.Clean;

        public FakeAiService() : base(
            new Mock<HttpClient>().Object,
            new Mock<IConfiguration>().Object,
            new Mock<ILogger<AiService>>().Object)
        {
        }

        public override Task<AiModerationVerdict> ClassifyAsync(string text)
        {
            return Task.FromResult(Verdict);
        }
    }

    public class ModerationWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public ModerationWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Moderation:Enabled"] = "true",
                    ["Moderation:StrikeThreshold"] = "3",
                    ["Moderation:StrikeWindowMinutes"] = "10",
                    ["Moderation:MuteDurationMinutes"] = "15"
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AiService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<AiService, FakeAiService>();
            });
        }
    }

    [Collection("DatabaseCollection")]
    public class ModerationIntegrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly ModerationWebApplicationFactory _factory;
        private readonly UserRepository _userRepository;
        private readonly ModerationRepository _moderationRepository;
        private readonly RoomRepository _roomRepository;

        public ModerationIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _factory = new ModerationWebApplicationFactory(fixture.ConnectionString);

            var context = fixture.DapperContext ?? throw new InvalidOperationException("DapperContext is null");
            _userRepository = new UserRepository(context);
            _moderationRepository = new ModerationRepository(context);
            _roomRepository = new RoomRepository(context);
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
            FakeAiService.Verdict = AiModerationVerdict.Clean;
        }

        public async Task DisposeAsync()
        {
            await _factory.DisposeAsync();
        }

        private string GenerateToken(User user)
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();
                return tokenService.CreateToken(user);
            }
        }

        private HubConnection CreateHubConnection(string token)
        {
            var handler = _factory.Server.CreateHandler();

            return new HubConnectionBuilder()
                .WithUrl("http://localhost/chatHub", options =>
                {
                    options.HttpMessageHandlerFactory = _ => handler;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    options.Transports = HttpTransportType.LongPolling;
                })
                .Build();
        }

        [Fact]
        public async Task SendMessage_WhenClean_ShouldSucceed()
        {
            // Arrange
            var user = new User { Username = "anphuc", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var token = GenerateToken(user);

            var connection = CreateHubConnection(token);
            await connection.StartAsync();

            // Seed room
            using (var db = _fixture.DapperContext.CreateConnection())
            {
                await db.ExecuteAsync("INSERT INTO chat_rooms (id, name) VALUES (10, 'Test Room') ON CONFLICT DO NOTHING;");
            }

            // Act & Assert
            await connection.InvokeAsync("SendMessage", 10, "Tin nhan sach", (int?)null, (string?)null, (string?)null, (string?)null, (long?)null, (string?)null);
            await connection.StopAsync();
        }

        [Fact]
        public async Task SendMessage_WhenHitMaskWord_ShouldSaveMaskedText()
        {
            // Arrange
            var user = new User { Username = "user_mask", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var token = GenerateToken(user);

            // Banned words list has "fuck" as mask tier (already seeded in DbInitializer)
            var connection = CreateHubConnection(token);
            await connection.StartAsync();

            using (var db = _fixture.DapperContext.CreateConnection())
            {
                await db.ExecuteAsync("INSERT INTO chat_rooms (id, name) VALUES (11, 'Test Room') ON CONFLICT DO NOTHING;");
            }

            // Act
            await connection.InvokeAsync("SendMessage", 11, "con cho fuck", (int?)null, (string?)null, (string?)null, (string?)null, (long?)null, (string?)null);
            await connection.StopAsync();

            // Assert
            using (var db = _fixture.DapperContext.CreateConnection())
            {
                var msg = await db.QueryFirstOrDefaultAsync<Message>("SELECT * FROM messages WHERE sender_name = 'user_mask' LIMIT 1;");
                Assert.NotNull(msg);
                Assert.Equal("con cho ***", msg.Content);
            }
        }

        [Fact]
        public async Task SendMessage_WhenHitSuspectWord_AndAIToxic_ShouldThrowHubException_AndNotSave()
        {
            // Arrange
            var user = new User { Username = "user_suspect", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var token = GenerateToken(user);

            FakeAiService.Verdict = AiModerationVerdict.Toxic;

            var connection = CreateHubConnection(token);
            await connection.StartAsync();

            using (var db = _fixture.DapperContext.CreateConnection())
            {
                await db.ExecuteAsync("INSERT INTO chat_rooms (id, name) VALUES (12, 'Test Room') ON CONFLICT DO NOTHING;");
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
                connection.InvokeAsync("SendMessage", 12, "tao se chem may", (int?)null, (string?)null, (string?)null, (string?)null, (long?)null, (string?)null)
            );

            Assert.Contains("vi phạm chuẩn mực", exception.Message);

            // Verify no message was saved
            using (var db = _fixture.DapperContext.CreateConnection())
            {
                var msgCount = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM messages WHERE sender_name = 'user_suspect';");
                Assert.Equal(0, msgCount);

                // Verify moderation log is recorded
                var logCount = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM moderation_logs WHERE username = 'user_suspect';");
                Assert.Equal(1, logCount);
            }

            await connection.StopAsync();
        }

        [Fact]
        public async Task EditMessage_WhenNewContentIsToxic_ShouldBlockEdit()
        {
            // Arrange
            var user = new User { Username = "user_edit", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var token = GenerateToken(user);

            var connection = CreateHubConnection(token);
            await connection.StartAsync();

            using (var db = _fixture.DapperContext.CreateConnection())
            {
                await db.ExecuteAsync("INSERT INTO chat_rooms (id, name) VALUES (13, 'Test Room') ON CONFLICT DO NOTHING;");
                await db.ExecuteAsync("INSERT INTO messages (id, room_id, sender_id, sender_name, content) VALUES (100, 13, @userId, 'user_edit', 'Tin nhan sach');", new { userId = user.Id });
            }

            // Act & Assert: try editing message to toxic
            FakeAiService.Verdict = AiModerationVerdict.Toxic;

            var exception = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
                connection.InvokeAsync("EditMessage", 100, "tao se chem may")
            );

            Assert.Contains("vi phạm chuẩn mực", exception.Message);

            // Verify original message content is NOT modified
            using (var db = _fixture.DapperContext.CreateConnection())
            {
                var originalMsg = await db.QueryFirstOrDefaultAsync<Message>("SELECT * FROM messages WHERE id = 100;");
                Assert.NotNull(originalMsg);
                Assert.Equal("Tin nhan sach", originalMsg.Content);
            }

            await connection.StopAsync();
        }

        [Fact]
        public async Task AutoMute_WhenThresholdReached_ShouldMuteUser()
        {
            // Arrange
            var user = new User { Username = "user_mute", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var token = GenerateToken(user);

            FakeAiService.Verdict = AiModerationVerdict.Toxic;

            var connection = CreateHubConnection(token);
            await connection.StartAsync();

            using (var db = _fixture.DapperContext.CreateConnection())
            {
                await db.ExecuteAsync("INSERT INTO chat_rooms (id, name) VALUES (14, 'Test Room') ON CONFLICT DO NOTHING;");
            }

            // Act: send toxic messages repeatedly (threshold = 3)
            for (int i = 0; i < 3; i++)
            {
                await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
                    connection.InvokeAsync("SendMessage", 14, "tao se chem may", (int?)null, (string?)null, (string?)null, (string?)null, (long?)null, (string?)null)
                );
            }

            // The 4th message should fail with mute message instead of toxic message
            var muteException = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
                connection.InvokeAsync("SendMessage", 14, "Tin nhan binh thuong", (int?)null, (string?)null, (string?)null, (string?)null, (long?)null, (string?)null)
            );

            Assert.Contains("tạm hạn chế gửi tin", muteException.Message);

            await connection.StopAsync();
        }

        [Fact]
        public async Task Register_WhenUsernameIsToxic_ShouldReturn400BadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var registerDto = new { Username = "chem_admin", Password = "password" }; // "chem" is suspect
            FakeAiService.Verdict = AiModerationVerdict.Toxic;

            // Act
            var response = await client.PostAsJsonAsync("/api/auth/register", registerDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Tên đăng ký không hợp lệ", content);
        }

        [Fact]
        public async Task ModerationController_Endpoints_ShouldBeRestrictedToAdmin()
        {
            // Arrange
            var normalUser = new User { Username = "normal_user", PasswordHash = "hash", IsAdmin = false };
            await _userRepository.Create(normalUser);
            var normalToken = GenerateToken(normalUser);

            var adminUser = new User { Username = "admin_user", PasswordHash = "hash", IsAdmin = true };
            await _userRepository.Create(adminUser);
            using (var db = _fixture.DapperContext.CreateConnection())
            {
                await db.ExecuteAsync("UPDATE users SET is_admin = TRUE WHERE id = @id", new { id = adminUser.Id });
            }
            var adminToken = GenerateToken(adminUser);

            var client = _factory.CreateClient();

            // 1. Non-admin calls GET api/moderation/words -> 403 Forbidden
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", normalToken);
            var response403 = await client.GetAsync("/api/moderation/words");
            Assert.Equal(HttpStatusCode.Forbidden, response403.StatusCode);

            // 2. Admin calls GET api/moderation/words -> 200 OK
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var response200 = await client.GetAsync("/api/moderation/words");
            Assert.Equal(HttpStatusCode.OK, response200.StatusCode);
        }
    }
}
