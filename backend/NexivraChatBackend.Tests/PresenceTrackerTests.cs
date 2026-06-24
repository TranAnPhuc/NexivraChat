using System.Linq;
using NexivraChatBackend.Services;
using Xunit;

namespace NexivraChatBackend.Tests
{
    public class PresenceTrackerTests
    {
        [Fact]
        public void GetOnlineUsers_EmptyRoom_ReturnsEmpty()
        {
            var tracker = new PresenceTracker();
            Assert.Empty(tracker.GetOnlineUsers(1));
        }

        [Fact]
        public void UserJoined_AddsUserToRoom()
        {
            var tracker = new PresenceTracker();
            tracker.UserJoined(1, "conn-a", "alice");
            Assert.Equal(new[] { "alice" }, tracker.GetOnlineUsers(1));
        }

        [Fact]
        public void GetOnlineUsers_ReturnsDistinctSorted()
        {
            var tracker = new PresenceTracker();
            tracker.UserJoined(1, "conn-b", "bob");
            tracker.UserJoined(1, "conn-a", "alice");
            Assert.Equal(new[] { "alice", "bob" }, tracker.GetOnlineUsers(1));
        }

        [Fact]
        public void MultipleConnections_SameUser_CountsOnceUntilAllLeft()
        {
            var tracker = new PresenceTracker();
            tracker.UserJoined(1, "conn-1", "alice");
            tracker.UserJoined(1, "conn-2", "alice"); // alice mở 2 tab
            Assert.Equal(new[] { "alice" }, tracker.GetOnlineUsers(1));

            tracker.UserLeft(1, "conn-1", "alice"); // đóng 1 tab
            Assert.Equal(new[] { "alice" }, tracker.GetOnlineUsers(1)); // vẫn online

            tracker.UserLeft(1, "conn-2", "alice"); // đóng nốt tab còn lại
            Assert.Empty(tracker.GetOnlineUsers(1)); // giờ mới offline
        }

        [Fact]
        public void RemoveConnection_RemovesFromAllRooms_ReturnsAffectedRoomIds()
        {
            var tracker = new PresenceTracker();
            tracker.UserJoined(1, "conn-x", "alice");
            tracker.UserJoined(2, "conn-x", "alice"); // cùng connection ở 2 phòng

            var affected = tracker.RemoveConnection("conn-x");

            Assert.Equal(new[] { 1, 2 }, affected.OrderBy(r => r).ToArray());
            Assert.Empty(tracker.GetOnlineUsers(1));
            Assert.Empty(tracker.GetOnlineUsers(2));
        }

        [Fact]
        public void UserLeft_UnknownConnection_DoesNotThrow()
        {
            var tracker = new PresenceTracker();
            tracker.UserLeft(1, "ghost", "nobody"); // không ném lỗi
            Assert.Empty(tracker.GetOnlineUsers(1));
        }
    }
}
