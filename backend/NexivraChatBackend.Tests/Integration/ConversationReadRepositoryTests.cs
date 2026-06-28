using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Xunit;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Tests.Fixtures;

namespace NexivraChatBackend.Tests.Integration
{
    [Collection("DatabaseCollection")]
    public class ConversationReadRepositoryTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly ConversationReadRepository _repository;
        private readonly UserRepository _userRepository;
        private readonly RoomRepository _roomRepository;
        private readonly PrivateChatRepository _privateChatRepository;
        private readonly MessageRepository _messageRepository;

        public ConversationReadRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            var context = fixture.DapperContext ?? throw new InvalidOperationException("DapperContext is null");
            _repository = new ConversationReadRepository(context);
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
        public async Task GetUnreadCounts_ShouldMatchExpectedJoinBehavior_ForRoomsAndDms()
        {
            // 1. Arrange: Seed Users
            var userA = new User { Username = "UserA", PasswordHash = "hash" };
            var userB = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(userA);
            await _userRepository.Create(userB);

            // Seed Rooms
            var room1 = new ChatRoom { Name = "General Room", Description = "General" };
            var room2 = new ChatRoom { Name = "AI Lounge Room", Description = "AI" };
            await _roomRepository.Create(room1);
            await _roomRepository.Create(room2);

            // Seed Private Chat (DM) between UserA and UserB
            var dmChat = await _privateChatRepository.GetOrCreate(userA.Id, userB.Id);

            // Send messages in Room 1 (sent by UserB)
            var msgRoom1 = new Message
            {
                RoomId = room1.Id,
                SenderName = userB.Username,
                Content = "Hello Room 1",
                CreatedAt = DateTime.UtcNow,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msgRoom1);

            // Send messages in DM (sent by UserB)
            var msgDm1 = new Message
            {
                PrivateChatId = dmChat.Id,
                SenderName = userB.Username,
                Content = "Hello DM 1",
                CreatedAt = DateTime.UtcNow,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msgDm1);

            var msgDm2 = new Message
            {
                PrivateChatId = dmChat.Id,
                SenderName = userB.Username,
                Content = "Hello DM 2",
                CreatedAt = DateTime.UtcNow,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msgDm2);

            // 2. Act: Get unread counts for UserA
            var unreadCounts = await _repository.GetUnreadCounts(userA.Id, userA.Username);

            // 3. Assert:
            // - Public Room: UserA has never opened the room (no read history in conversation_reads),
            //   so unread count must be 0 due to INNER JOIN.
            Assert.False(unreadCounts.Rooms.ContainsKey(room1.Id));

            // - DM: UserA has never opened the DM, but count must still be 2 due to LEFT JOIN + COALESCE.
            //   The key in PrivateChats is UserB.Id (the partner user's ID).
            Assert.True(unreadCounts.PrivateChats.ContainsKey(userB.Id));
            Assert.Equal(2, unreadCounts.PrivateChats[userB.Id]);

            // Now, simulate UserA marks the room read up to msgRoom1.Id
            await _repository.MarkRoomRead(userA.Id, room1.Id, msgRoom1.Id);

            // Send another message in Room 1
            var msgRoom2 = new Message
            {
                RoomId = room1.Id,
                SenderName = userB.Username,
                Content = "Second msg Room 1",
                CreatedAt = DateTime.UtcNow,
                IsAi = false
            };
            await _messageRepository.SaveNewMessage(msgRoom2);

            // Query counts again
            unreadCounts = await _repository.GetUnreadCounts(userA.Id, userA.Username);

            // Now the room should have 1 unread message
            Assert.True(unreadCounts.Rooms.ContainsKey(room1.Id));
            Assert.Equal(1, unreadCounts.Rooms[room1.Id]);

            // Simulate UserA marks DM read up to msgDm1.Id
            await _repository.MarkPrivateChatRead(userA.Id, dmChat.Id, msgDm1.Id);

            unreadCounts = await _repository.GetUnreadCounts(userA.Id, userA.Username);

            // DM should now have only 1 unread message (msgDm2)
            Assert.True(unreadCounts.PrivateChats.ContainsKey(userB.Id));
            Assert.Equal(1, unreadCounts.PrivateChats[userB.Id]);
        }

        [Fact]
        public async Task MarkRead_ShouldUseGreatestFunctionToPreventLowerMessageIdOverwrite()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);

            var room = new ChatRoom { Name = "General Room", Description = "General" };
            await _roomRepository.Create(room);

            // Act 1: Mark room read at message ID 10
            await _repository.MarkRoomRead(user.Id, room.Id, 10);

            // Verify message ID is 10
            var readId = await GetLastReadMessageId(user.Id, roomId: room.Id, privateChatId: null);
            Assert.Equal(10, readId);

            // Act 2: Mark room read at message ID 5 (older message/out of order)
            await _repository.MarkRoomRead(user.Id, room.Id, 5);

            // Assert: The message ID must still be 10 (GREATEST(10, 5) = 10)
            readId = await GetLastReadMessageId(user.Id, roomId: room.Id, privateChatId: null);
            Assert.Equal(10, readId);

            // Act 3: Mark room read at message ID 15 (newer message)
            await _repository.MarkRoomRead(user.Id, room.Id, 15);

            // Assert: The message ID must be updated to 15 (GREATEST(10, 15) = 15)
            readId = await GetLastReadMessageId(user.Id, roomId: room.Id, privateChatId: null);
            Assert.Equal(15, readId);
        }

        [Fact]
        public async Task DatabaseSchema_ShouldEnforceUniqueConstraints_OnConversationReads()
        {
            // Arrange
            var user = new User { Username = "UserA", PasswordHash = "hash" };
            await _userRepository.Create(user);

            var room = new ChatRoom { Name = "General Room", Description = "General" };
            await _roomRepository.Create(room);

            var dmUser = new User { Username = "UserB", PasswordHash = "hash" };
            await _userRepository.Create(dmUser);
            var dm = await _privateChatRepository.GetOrCreate(user.Id, dmUser.Id);

            // Act & Assert 1: Test room unique constraint uq_reads_room
            using (var connection = _fixture.DapperContext!.CreateConnection())
            {
                var insertQuery = @"
                    INSERT INTO conversation_reads (user_id, room_id, last_read_message_id)
                    VALUES (@userId, @roomId, 10);";

                await connection.ExecuteAsync(insertQuery, new { userId = user.Id, roomId = room.Id });

                // Inserting the exact same row should fail
                var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
                {
                    await connection.ExecuteAsync(insertQuery, new { userId = user.Id, roomId = room.Id });
                });

                Assert.Equal("uq_reads_room", ex.ConstraintName);
            }

            // Act & Assert 2: Test DM unique constraint uq_reads_dm
            using (var connection = _fixture.DapperContext!.CreateConnection())
            {
                var insertQuery = @"
                    INSERT INTO conversation_reads (user_id, private_chat_id, last_read_message_id)
                    VALUES (@userId, @dmId, 10);";

                await connection.ExecuteAsync(insertQuery, new { userId = user.Id, dmId = dm.Id });

                // Inserting the exact same row should fail
                var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
                {
                    await connection.ExecuteAsync(insertQuery, new { userId = user.Id, dmId = dm.Id });
                });

                Assert.Equal("uq_reads_dm", ex.ConstraintName);
            }
        }

        private async Task<int?> GetLastReadMessageId(int userId, int? roomId, int? privateChatId)
        {
            using (var connection = _fixture.DapperContext!.CreateConnection())
            {
                var query = @"
                    SELECT last_read_message_id
                    FROM conversation_reads
                    WHERE user_id = @userId
                      AND (room_id = @roomId OR (room_id IS NULL AND @roomId IS NULL))
                      AND (private_chat_id = @privateChatId OR (private_chat_id IS NULL AND @privateChatId IS NULL));";

                return await connection.QueryFirstOrDefaultAsync<int?>(query, new { userId, roomId, privateChatId });
            }
        }
    }
}
