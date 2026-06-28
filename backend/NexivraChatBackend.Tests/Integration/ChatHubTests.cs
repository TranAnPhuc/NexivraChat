using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;
using NexivraChatBackend.Tests.Fixtures;

namespace NexivraChatBackend.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public CustomWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString
                });
            });
        }
    }

    [Collection("DatabaseCollection")]
    public class ChatHubTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly CustomWebApplicationFactory _factory;
        private readonly UserRepository _userRepository;
        private readonly PrivateChatRepository _privateChatRepository;
        private readonly MessageRepository _messageRepository;

        public ChatHubTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _factory = new CustomWebApplicationFactory(fixture.ConnectionString);

            var context = fixture.DapperContext ?? throw new InvalidOperationException("DapperContext is null");
            _userRepository = new UserRepository(context);
            _privateChatRepository = new PrivateChatRepository(context);
            _messageRepository = new MessageRepository(context);
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
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
        public async Task MarkRead_ForDm_ShouldThrowHubException_WhenUserIsNotParticipant()
        {
            // 1. Arrange: Seed Users
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            var userC = new User { Username = "UserC", PasswordHash = "hash" }; // Non-participant
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);
            await _userRepository.Create(userC);

            // Create Private Chat between A & B
            var dm = await _privateChatRepository.GetOrCreate(userA.Id, userB.Id);

            // Seed a message in the private chat
            var msg = new Message
            {
                PrivateChatId = dm.Id,
                SenderName = userA.Username,
                Content = "Hello UserB",
                CreatedAt = DateTime.UtcNow,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msg);

            // Generate Token for UserC (not a participant)
            var tokenC = GenerateToken(userC);

            // Connect UserC SignalR client
            var connectionC = CreateHubConnection(tokenC);
            await connectionC.StartAsync();

            // 2. Act & Assert: Call MarkRead as UserC on A & B's conversation and expect HubException
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
            {
                await connectionC.InvokeAsync("MarkRead", (int?)null, dm.Id, msg.Id);
            });

            Assert.Contains("Bạn không có quyền với hội thoại này.", ex.Message);

            // Clean up connection
            await connectionC.StopAsync();
            await connectionC.DisposeAsync();
        }

        [Fact]
        public async Task MarkRead_ForDm_ShouldSucceedAndRecordRead_WhenUserIsParticipant()
        {
            // 1. Arrange: Seed Users
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);

            // Create Private Chat between A & B
            var dm = await _privateChatRepository.GetOrCreate(userA.Id, userB.Id);

            // Seed a message in the private chat (from A to B)
            var msg = new Message
            {
                PrivateChatId = dm.Id,
                SenderName = userA.Username,
                Content = "Hello UserB",
                CreatedAt = DateTime.UtcNow,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msg);

            // Generate Token for UserB (valid participant)
            var tokenB = GenerateToken(userB);

            // Connect UserB SignalR client
            var connectionB = CreateHubConnection(tokenB);
            await connectionB.StartAsync();

            // 2. Act: Call MarkRead as UserB
            await connectionB.InvokeAsync("MarkRead", (int?)null, dm.Id, msg.Id);

            // 3. Assert: Check database to ensure read history is recorded
            using (var connection = _fixture.DapperContext!.CreateConnection())
            {
                var query = @"
                    SELECT last_read_message_id
                    FROM conversation_reads
                    WHERE user_id = @userId AND private_chat_id = @privateChatId;";

                var lastReadId = await connection.QueryFirstOrDefaultAsync<int?>(query, new
                {
                    userId = userB.Id,
                    privateChatId = dm.Id
                });

                Assert.NotNull(lastReadId);
                Assert.Equal(msg.Id, lastReadId.Value);
            }

            // Clean up connection
            await connectionB.StopAsync();
            await connectionB.DisposeAsync();
        }

        [Fact]
        public async Task ToggleReaction_NonParticipantDm_ThrowsHubException()
        {
            // Arrange
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            var userC = new User { Username = "UserC", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);
            await _userRepository.Create(userC);

            var dm = await _privateChatRepository.GetOrCreate(userA.Id, userB.Id);
            var msg = new Message { PrivateChatId = dm.Id, SenderId = userA.Id, SenderName = userA.Username, Content = "Private DM", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(msg);

            var tokenC = GenerateToken(userC);
            var connectionC = CreateHubConnection(tokenC);
            await connectionC.StartAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
            {
                await connectionC.InvokeAsync("ToggleReaction", msg.Id, "👍");
            });
            Assert.Contains("Bạn không có quyền với hội thoại này.", ex.Message);

            await connectionC.StopAsync();
            await connectionC.DisposeAsync();
        }

        [Fact]
        public async Task EditMessage_NonOwner_ThrowsHubException()
        {
            // Arrange
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);

            var dm = await _privateChatRepository.GetOrCreate(userA.Id, userB.Id);
            var msg = new Message { PrivateChatId = dm.Id, SenderId = userA.Id, SenderName = userA.Username, Content = "Original DM", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(msg);

            var tokenB = GenerateToken(userB);
            var connectionB = CreateHubConnection(tokenB);
            await connectionB.StartAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
            {
                await connectionB.InvokeAsync("EditMessage", msg.Id, "Hacked by B");
            });
            Assert.Contains("Không có quyền hoặc tin không tồn tại.", ex.Message);

            await connectionB.StopAsync();
            await connectionB.DisposeAsync();
        }

        [Fact]
        public async Task DeleteMessage_NonOwner_ThrowsHubException()
        {
            // Arrange
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);

            var dm = await _privateChatRepository.GetOrCreate(userA.Id, userB.Id);
            var msg = new Message { PrivateChatId = dm.Id, SenderId = userA.Id, SenderName = userA.Username, Content = "Original DM", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(msg);

            var tokenB = GenerateToken(userB);
            var connectionB = CreateHubConnection(tokenB);
            await connectionB.StartAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
            {
                await connectionB.InvokeAsync("DeleteMessage", msg.Id);
            });
            Assert.Contains("Không có quyền hoặc tin không tồn tại.", ex.Message);

            await connectionB.StopAsync();
            await connectionB.DisposeAsync();
        }
    }
}
