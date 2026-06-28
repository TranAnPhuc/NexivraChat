using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Tests.Fixtures;

namespace NexivraChatBackend.Tests.Integration
{
    [Collection("DatabaseCollection")]
    public class MentionRepositoryTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly MentionRepository _mentionRepository;
        private readonly UserRepository _userRepository;
        private readonly RoomRepository _roomRepository;
        private readonly MessageRepository _messageRepository;
        private readonly ConversationReadRepository _conversationReadRepository;

        public MentionRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            var context = fixture.DapperContext ?? throw new InvalidOperationException("DapperContext is null");
            _mentionRepository = new MentionRepository(context);
            _userRepository = new UserRepository(context);
            _roomRepository = new RoomRepository(context);
            _messageRepository = new MessageRepository(context);
            _conversationReadRepository = new ConversationReadRepository(context);
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task GetUnreadMentionRoomIds_ReturnsRoom_WhenMentionUnread()
        {
            // Arrange
            var sender = new User { Username = "sender_user", PasswordHash = "hash" };
            await _userRepository.Create(sender);

            var mentioned = new User { Username = "mentioned_user", PasswordHash = "hash" };
            await _userRepository.Create(mentioned);

            var room = new ChatRoom { Name = "General Room", Description = "Test Room" };
            await _roomRepository.Create(room);

            var message = new Message
            {
                RoomId = room.Id,
                SenderId = sender.Id,
                SenderName = sender.Username,
                Content = $"Hello @{mentioned.Username}",
                CreatedAt = DateTime.Now,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(message);
            await _mentionRepository.SaveMentions(message.Id, new[] { mentioned.Id });

            // Act
            var unreadRoomIds = await _mentionRepository.GetUnreadMentionRoomIds(mentioned.Id);

            // Assert
            Assert.Contains(room.Id, unreadRoomIds);
        }

        [Fact]
        public async Task GetUnreadMentionRoomIds_Empty_WhenReadPastMention()
        {
            // Arrange
            var sender = new User { Username = "sender_user2", PasswordHash = "hash" };
            await _userRepository.Create(sender);

            var mentioned = new User { Username = "mentioned_user2", PasswordHash = "hash" };
            await _userRepository.Create(mentioned);

            var room = new ChatRoom { Name = "Tech Room", Description = "Test Room" };
            await _roomRepository.Create(room);

            var message = new Message
            {
                RoomId = room.Id,
                SenderId = sender.Id,
                SenderName = sender.Username,
                Content = $"Hi @{mentioned.Username}",
                CreatedAt = DateTime.Now,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(message);
            await _mentionRepository.SaveMentions(message.Id, new[] { mentioned.Id });

            // User 2 reads past the mention
            await _conversationReadRepository.MarkRoomRead(mentioned.Id, room.Id, message.Id);

            // Act
            var unreadRoomIds = await _mentionRepository.GetUnreadMentionRoomIds(mentioned.Id);

            // Assert
            Assert.DoesNotContain(room.Id, unreadRoomIds);
        }
    }
}
