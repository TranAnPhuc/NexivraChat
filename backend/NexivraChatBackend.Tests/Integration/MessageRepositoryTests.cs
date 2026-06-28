using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Xunit;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Tests.Fixtures;

namespace NexivraChatBackend.Tests.Integration
{
    [Collection("DatabaseCollection")]
    public class MessageRepositoryTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly MessageRepository _repository;
        private readonly UserRepository _userRepository;
        private readonly RoomRepository _roomRepository;
        private readonly PrivateChatRepository _privateChatRepository;

        public MessageRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            var context = fixture.DapperContext ?? throw new InvalidOperationException("DapperContext is null");
            _repository = new MessageRepository(context);
            _userRepository = new UserRepository(context);
            _roomRepository = new RoomRepository(context);
            _privateChatRepository = new PrivateChatRepository(context);
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task EditMessage_OwnMessage_UpdatesContent_AndSetsEditedAt()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Original", CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(msg);

            // Act
            var affected = await _repository.EditMessage(msg.Id, user.Id, "Updated Content");

            // Assert
            Assert.Equal(1, affected);
            var updated = await _repository.GetById(msg.Id);
            Assert.NotNull(updated);
            Assert.Equal("Updated Content", updated.Content);
            Assert.NotNull(updated.EditedAt);
        }

        [Fact]
        public async Task EditMessage_OtherUsersMessage_ReturnsZeroAffected()
        {
            // Arrange
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderId = userA.Id, SenderName = userA.Username, Content = "UserA's message", CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(msg);

            // Act: UserB tries to edit UserA's message
            var affected = await _repository.EditMessage(msg.Id, userB.Id, "Hacked Content");

            // Assert
            Assert.Equal(0, affected);
            var original = await _repository.GetById(msg.Id);
            Assert.NotNull(original);
            Assert.Equal("UserA's message", original.Content);
            Assert.Null(original.EditedAt);
        }

        [Fact]
        public async Task EditMessage_AlreadyDeleted_ReturnsZeroAffected()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "To be deleted", CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(msg);

            await _repository.SoftDeleteMessage(msg.Id, user.Id);

            // Act
            var affected = await _repository.EditMessage(msg.Id, user.Id, "Edit deleted");

            // Assert
            Assert.Equal(0, affected);
        }

        [Fact]
        public async Task SoftDeleteMessage_OwnMessage_SetsDeletedAt()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Delete me", CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(msg);

            // Act
            var affected = await _repository.SoftDeleteMessage(msg.Id, user.Id);

            // Assert
            Assert.Equal(1, affected);
            var fetched = await _repository.GetById(msg.Id);
            Assert.NotNull(fetched);
            Assert.NotNull(fetched.DeletedAt);
        }

        [Fact]
        public async Task SoftDeleteMessage_OtherUsersMessage_ReturnsZeroAffected()
        {
            // Arrange
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderId = userA.Id, SenderName = userA.Username, Content = "UserA content", CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(msg);

            // Act
            var affected = await _repository.SoftDeleteMessage(msg.Id, userB.Id);

            // Assert
            Assert.Equal(0, affected);
            var fetched = await _repository.GetById(msg.Id);
            Assert.NotNull(fetched);
            Assert.Null(fetched.DeletedAt);
        }

        [Fact]
        public async Task GetMessagesByRoom_DeletedMessage_BlanksContent()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Confidential Top Secret", CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(msg);

            await _repository.SoftDeleteMessage(msg.Id, user.Id);

            // Act
            var messages = await _repository.GetMessagesByRoom(room.Id);

            // Assert
            Assert.Single(messages);
            var fetched = messages.First();
            Assert.Equal("", fetched.Content);
            Assert.NotNull(fetched.DeletedAt);
        }

        [Fact]
        public async Task GetMessagesByRoom_ReplySnapshot_PopulatesSenderAndTruncatedContent()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);

            var originalMsg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = new string('A', 150), CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(originalMsg);

            var replyMsg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "I agree", ReplyToId = originalMsg.Id, CreatedAt = DateTime.UtcNow.AddSeconds(1) };
            await _repository.SaveNewMessage(replyMsg);

            // Act
            var messages = await _repository.GetMessagesByRoom(room.Id);

            // Assert
            var fetchedReply = messages.FirstOrDefault(m => m.Id == replyMsg.Id);
            Assert.NotNull(fetchedReply);
            Assert.Equal(user.Username, fetchedReply.ReplyToSenderName);
            Assert.Equal(120, fetchedReply.ReplyToContent?.Length);
        }

        [Fact]
        public async Task GetMessagesByRoom_ReplyToDeleted_SnapshotContentNull()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);

            var originalMsg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Original Secret", CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(originalMsg);

            var replyMsg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Replying", ReplyToId = originalMsg.Id, CreatedAt = DateTime.UtcNow.AddSeconds(1) };
            await _repository.SaveNewMessage(replyMsg);

            await _repository.SoftDeleteMessage(originalMsg.Id, user.Id);

            // Act
            var messages = await _repository.GetMessagesByRoom(room.Id);

            // Assert
            var fetchedReply = messages.FirstOrDefault(m => m.Id == replyMsg.Id);
            Assert.NotNull(fetchedReply);
            Assert.Null(fetchedReply.ReplyToContent);
        }

        [Fact]
        public async Task GetMessagesByRoom_BeforeIdAndAfterId_KeysetPaginationWorks()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);

            var msg1 = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Msg1", CreatedAt = DateTime.UtcNow };
            await _repository.SaveNewMessage(msg1);
            var msg2 = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Msg2", CreatedAt = DateTime.UtcNow.AddSeconds(1) };
            await _repository.SaveNewMessage(msg2);
            var msg3 = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Msg3", CreatedAt = DateTime.UtcNow.AddSeconds(2) };
            await _repository.SaveNewMessage(msg3);

            // Act 1: Get messages before msg3
            var olderMessages = await _repository.GetMessagesByRoom(room.Id, limit: 10, beforeId: msg3.Id);
            Assert.Equal(2, olderMessages.Count);
            Assert.Equal(msg1.Id, olderMessages[0].Id);
            Assert.Equal(msg2.Id, olderMessages[1].Id);

            // Act 2: Get messages after msg1
            var newerMessages = await _repository.GetMessagesByRoom(room.Id, limit: 10, afterId: msg1.Id);
            Assert.Equal(2, newerMessages.Count);
            Assert.Equal(msg2.Id, newerMessages[0].Id);
            Assert.Equal(msg3.Id, newerMessages[1].Id);
        }

        [Fact]
        public async Task SaveNewMessage_SetsSenderId()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);

            var msg = new Message { RoomId = room.Id, SenderId = user.Id, SenderName = user.Username, Content = "Test SenderId", CreatedAt = DateTime.UtcNow };

            // Act
            await _repository.SaveNewMessage(msg);

            // Assert
            Assert.True(msg.Id > 0);
            var fetched = await _repository.GetById(msg.Id);
            Assert.NotNull(fetched);
            Assert.Equal(user.Id, fetched.SenderId);
        }
    }
}
