using System.Collections.Generic;
using System.Linq;

namespace NexivraChatBackend.Services
{
    /// <summary>
    /// Theo dõi presence trong bộ nhớ: phòng nào có những connection/username nào đang online.
    /// Một user có thể mở nhiều tab (nhiều connectionId); chỉ offline khi tất cả connection rời đi.
    /// An toàn đa luồng nhờ lock toàn cục.
    /// </summary>
    public class PresenceTracker
    {
        private readonly object _lock = new object();
        // roomId -> (connectionId -> username)
        private readonly Dictionary<int, Dictionary<string, string>> _rooms
            = new Dictionary<int, Dictionary<string, string>>();

        public void UserJoined(int roomId, string connectionId, string username)
        {
            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out var conns))
                {
                    conns = new Dictionary<string, string>();
                    _rooms[roomId] = conns;
                }
                conns[connectionId] = username;
            }
        }

        public void UserLeft(int roomId, string connectionId, string username)
        {
            lock (_lock)
            {
                if (_rooms.TryGetValue(roomId, out var conns))
                {
                    conns.Remove(connectionId);
                    if (conns.Count == 0)
                    {
                        _rooms.Remove(roomId);
                    }
                }
            }
        }

        public int[] RemoveConnection(string connectionId)
        {
            lock (_lock)
            {
                var affected = new List<int>();
                foreach (var roomId in _rooms.Keys.ToList())
                {
                    var conns = _rooms[roomId];
                    if (conns.Remove(connectionId))
                    {
                        affected.Add(roomId);
                        if (conns.Count == 0)
                        {
                            _rooms.Remove(roomId);
                        }
                    }
                }
                return affected.ToArray();
            }
        }

        public string[] GetOnlineUsers(int roomId)
        {
            lock (_lock)
            {
                if (_rooms.TryGetValue(roomId, out var conns))
                {
                    return conns.Values.Distinct().OrderBy(u => u).ToArray();
                }
                return new string[0];
            }
        }

        // Global presence tracking
        private readonly Dictionary<string, string> _globalConnections 
            = new Dictionary<string, string>();

        public void AddGlobalConnection(string connectionId, string username)
        {
            lock (_lock)
            {
                _globalConnections[connectionId] = username;
            }
        }

        public void RemoveGlobalConnection(string connectionId)
        {
            lock (_lock)
            {
                _globalConnections.Remove(connectionId);
            }
        }

        public string[] GetGlobalOnlineUsers()
        {
            lock (_lock)
            {
                return _globalConnections.Values.Distinct().OrderBy(u => u).ToArray();
            }
        }
    }
}
