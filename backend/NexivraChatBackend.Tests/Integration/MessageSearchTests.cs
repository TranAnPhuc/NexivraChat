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
    public class MessageSearchTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly UserRepository _userRepository;
        private readonly RoomRepository _roomRepository;
        private readonly MessageRepository _messageRepository;

        public MessageSearchTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            var context = fixture.DapperContext ?? throw new InvalidOperationException("DapperContext is null");
            _userRepository = new UserRepository(context);
            _roomRepository = new RoomRepository(context);
            _messageRepository = new MessageRepository(context);
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SearchRoomMessages_FindsMatch_CaseInsensitive()
        {
            // Arrange
            var sender = new User { Username = "search_user1", PasswordHash = "hash" };
            await _userRepository.Create(sender);

            var room = new ChatRoom { Name = "Search Room 1", Description = "Test Room" };
            await _roomRepository.Create(room);

            var msg = new Message
            {
                RoomId = room.Id,
                SenderId = sender.Id,
                SenderName = sender.Username,
                Content = "Xin chào cộng đồng NexivraChat Việt Nam",
                CreatedAt = DateTime.Now,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msg);

            // Act - Search lowercase "nexivrachat"
            var results = await _messageRepository.SearchRoomMessages(room.Id, "nexivrachat");

            // Assert
            Assert.Single(results);
            Assert.Equal(msg.Id, results[0].Id);
        }

        [Fact]
        public async Task SearchRoomMessages_ExcludesDeleted()
        {
            // Arrange
            var sender = new User { Username = "search_user2", PasswordHash = "hash" };
            await _userRepository.Create(sender);

            var room = new ChatRoom { Name = "Search Room 2", Description = "Test Room" };
            await _roomRepository.Create(room);

            var msg = new Message
            {
                RoomId = room.Id,
                SenderId = sender.Id,
                SenderName = sender.Username,
                Content = "Nội dung bí mật cần thu hồi",
                CreatedAt = DateTime.Now,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msg);

            // Soft delete message
            await _messageRepository.SoftDeleteMessage(msg.Id, sender.Id);

            // Act
            var results = await _messageRepository.SearchRoomMessages(room.Id, "bí mật");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchRoomMessages_EscapesWildcard()
        {
            // Arrange
            var sender = new User { Username = "search_user3", PasswordHash = "hash" };
            await _userRepository.Create(sender);

            var room = new ChatRoom { Name = "Search Room 3", Description = "Test Room" };
            await _roomRepository.Create(room);

            var msg1 = new Message
            {
                RoomId = room.Id,
                SenderId = sender.Id,
                SenderName = sender.Username,
                Content = "Giảm giá 100% ngày hôm nay!",
                CreatedAt = DateTime.Now,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msg1);

            var msg2 = new Message
            {
                RoomId = room.Id,
                SenderId = sender.Id,
                SenderName = sender.Username,
                Content = "Giảm giá 1000 mặt hàng hot",
                CreatedAt = DateTime.Now.AddSeconds(1),
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msg2);

            // Act - Search for literal "100%"
            var results = await _messageRepository.SearchRoomMessages(room.Id, "100%");

            // Assert
            Assert.Single(results);
            Assert.Equal(msg1.Id, results[0].Id);
        }
    }
}
