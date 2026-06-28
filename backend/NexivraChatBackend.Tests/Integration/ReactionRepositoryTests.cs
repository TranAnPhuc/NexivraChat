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
    public class ReactionRepositoryTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly ReactionRepository _repository;
        private readonly UserRepository _userRepository;
        private readonly RoomRepository _roomRepository;
        private readonly PrivateChatRepository _privateChatRepository;
        private readonly MessageRepository _messageRepository;

        public ReactionRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            var context = fixture.DapperContext ?? throw new InvalidOperationException("DapperContext is null");
            _repository = new ReactionRepository(context);
            _userRepository = new UserRepository(context);
            _roomRepository = new RoomRepository(context);
            _privateChatRepository = new PrivateChatRepository(context);
            _messageRepository = new MessageRepository(context);
        }

        public async Task InitializeAsync()
        {
            await _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task ToggleReaction_TwiceBySameUser_AddsThenRemoves()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderName = user.Username, Content = "Hello", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(msg);

            // Act 1: First toggle (Add reaction)
            var (reacted1, count1) = await _repository.ToggleReaction(msg.Id, user.Id, "👍");

            // Assert 1
            Assert.True(reacted1);
            Assert.Equal(1, count1);

            // Act 2: Second toggle (Remove reaction)
            var (reacted2, count2) = await _repository.ToggleReaction(msg.Id, user.Id, "👍");

            // Assert 2
            Assert.False(reacted2);
            Assert.Equal(0, count2);
        }

        [Fact]
        public async Task ToggleReaction_MultipleUsers_CountsAggregate()
        {
            // Arrange
            var user1 = new User { Username = "User1", PasswordHash = "hash" };
            var user2 = new User { Username = "User2", PasswordHash = "hash" };
            await _userRepository.Create(user1);
            await _userRepository.Create(user2);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderName = user1.Username, Content = "Test", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(msg);

            // Act
            await _repository.ToggleReaction(msg.Id, user1.Id, "❤️");
            var (reacted, count) = await _repository.ToggleReaction(msg.Id, user2.Id, "❤️");

            // Assert
            Assert.True(reacted);
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task GetReactionsForMessages_ReturnsCount_AndMineReactedTrueForSelf()
        {
            // Arrange
            var user1 = new User { Username = "User1", PasswordHash = "hash" };
            var user2 = new User { Username = "User2", PasswordHash = "hash" };
            await _userRepository.Create(user1);
            await _userRepository.Create(user2);
            var room = new ChatRoom { Name = "Room1", Description = "Desc" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderName = user1.Username, Content = "Hello", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(msg);

            await _repository.ToggleReaction(msg.Id, user1.Id, "😮");
            await _repository.ToggleReaction(msg.Id, user2.Id, "😮");

            // Act
            var result = await _repository.GetReactionsForMessages(new[] { msg.Id }, user1.Id);

            // Assert
            Assert.Single(result);
            var summary = result.First();
            Assert.Equal(msg.Id, summary.MessageId);
            Assert.Equal("😮", summary.Emoji);
            Assert.Equal(2, summary.Count);
            Assert.True(summary.MineReacted);
        }

        [Fact]
        public async Task GetReactionsForMessages_DmMessage_HiddenFromNonParticipant()
        {
            // Arrange: UserA and UserB in DM, UserC outside DM
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            var userC = new User { Username = "UserC", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);
            await _userRepository.Create(userC);

            var dm = await _privateChatRepository.GetOrCreate(userA.Id, userB.Id);
            var msg = new Message { PrivateChatId = dm.Id, SenderName = userA.Username, Content = "Secret DM", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(msg);

            await _repository.ToggleReaction(msg.Id, userA.Id, "👍");

            // Act 1: Query as Participant UserA
            var resultA = await _repository.GetReactionsForMessages(new[] { msg.Id }, userA.Id);
            Assert.Single(resultA);

            // Act 2: Query as Non-Participant UserC
            var resultC = await _repository.GetReactionsForMessages(new[] { msg.Id }, userC.Id);

            // Assert
            Assert.Empty(resultC);
        }

        [Fact]
        public async Task GetReactionsForMessages_RoomMessage_VisibleToAnyAuthenticatedUser()
        {
            // Arrange
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);

            var room = new ChatRoom { Name = "Public Room", Description = "Public" };
            await _roomRepository.Create(room);
            var msg = new Message { RoomId = room.Id, SenderName = userA.Username, Content = "Public Message", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(msg);

            await _repository.ToggleReaction(msg.Id, userA.Id, "🙏");

            // Act
            var resultB = await _repository.GetReactionsForMessages(new[] { msg.Id }, userB.Id);

            // Assert
            Assert.Single(resultB);
            Assert.Equal("🙏", resultB.First().Emoji);
            Assert.False(resultB.First().MineReacted);
        }

        [Fact]
        public async Task LookupConversation_ReturnsRoomId_OrPrivateChatId()
        {
            // Arrange
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);

            var room = new ChatRoom { Name = "Room", Description = "Desc" };
            await _roomRepository.Create(room);
            var dm = await _privateChatRepository.GetOrCreate(userA.Id, userB.Id);

            var roomMsg = new Message { RoomId = room.Id, SenderName = userA.Username, Content = "Room Msg", CreatedAt = DateTime.UtcNow };
            var dmMsg = new Message { PrivateChatId = dm.Id, SenderName = userA.Username, Content = "DM Msg", CreatedAt = DateTime.UtcNow };
            await _messageRepository.SaveNewMessage(roomMsg);
            await _messageRepository.SaveNewMessage(dmMsg);

            // Act
            var convRoom = await _repository.LookupConversation(roomMsg.Id);
            var convDm = await _repository.LookupConversation(dmMsg.Id);

            // Assert
            Assert.NotNull(convRoom);
            Assert.Equal(room.Id, convRoom.RoomId);
            Assert.Null(convRoom.PrivateChatId);

            Assert.NotNull(convDm);
            Assert.Equal(dm.Id, convDm.PrivateChatId);
            Assert.Null(convDm.RoomId);
        }
    }
}
