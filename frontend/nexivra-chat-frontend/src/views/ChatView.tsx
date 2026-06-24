/* eslint-disable react-hooks/set-state-in-effect */
/* eslint-disable react-hooks/exhaustive-deps */
/* eslint-disable @typescript-eslint/no-unused-vars */
import React, { useState, useEffect, useRef } from 'react';
import { Input, Button, message } from 'antd';
import { SendOutlined } from '@ant-design/icons';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import api, { API_BASE_URL } from '../services/api';
import { RoomSidebar, type Room } from '../components/RoomSidebar';
import { CopilotPanel } from '../components/CopilotPanel';

export interface Message {
  id: number;
  roomId: number;
  senderName: string;
  content: string;
  createdAt: string;
  isAi: boolean;
}

interface ChatViewProps {
  username: string;
  token: string;
  onLogout: () => void;
}

export const ChatView: React.FC<ChatViewProps> = ({ username, token, onLogout }) => {
  const [rooms, setRooms] = useState<Room[]>([]);
  const [activeRoomId, setActiveRoomId] = useState<number | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [inputText, setInputText] = useState('');
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [notifications, setNotifications] = useState<string[]>([]);
  const [onlineUsers, setOnlineUsers] = useState<string[]>([]);
  const [typingUsers, setTypingUsers] = useState<string[]>([]);
  const typingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isTypingRef = useRef(false);
  const connectionRef = useRef<HubConnection | null>(null);
  const activeRoomIdRef = useRef<number | null>(null);

  const messageEndRef = useRef<HTMLDivElement>(null);

  // 1. Tải danh sách phòng từ API
  const fetchRooms = async () => {
    try {
      const response = await api.get('/rooms');
      const roomsData = response.data;
      setRooms(roomsData);
      if (roomsData.length > 0 && activeRoomId === null) {
        setActiveRoomId(roomsData[0].id);
      }
    } catch (err) {
      message.error('Không thể lấy danh sách phòng chat.');
    }
  };

  useEffect(() => {
    fetchRooms();
  }, []);

  // 2. Tải lịch sử tin nhắn khi đổi phòng chat
  const fetchMessageHistory = async (roomId: number) => {
    try {
      const response = await api.get(`/rooms/${roomId}/messages?limit=50&offset=0`);
      setMessages(response.data);
    } catch (err) {
      message.error('Không thể lấy lịch sử tin nhắn.');
    }
  };

  useEffect(() => { connectionRef.current = connection; }, [connection]);
  useEffect(() => { activeRoomIdRef.current = activeRoomId; }, [activeRoomId]);

  useEffect(() => {
    if (activeRoomId !== null) {
      setOnlineUsers([]);
      setTypingUsers([]);
      // Clear any in-flight typing timer so it cannot fire against the stale room
      if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
      isTypingRef.current = false;
      fetchMessageHistory(activeRoomId);
      // Nếu SignalR đang kết nối, gửi yêu cầu tham gia phòng mới
      if (connection && connection.state === 'Connected') {
        connection.invoke('JoinRoom', activeRoomId)
          .catch((err) => console.error('JoinRoom error:', err));
      }
    }
  }, [activeRoomId]);

  // 3. Khởi tạo kết nối SignalR
  useEffect(() => {
    const hubUrl = `${API_BASE_URL.replace('/api', '')}/chatHub?access_token=${token}`;
    const newConnection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    setConnection(newConnection);
  }, [token]);

  // 4. Lắng nghe các sự kiện từ SignalR Hub
  useEffect(() => {
    if (connection) {
      connection.start()
        .then(() => {
          console.log('SignalR connected.');
          if (activeRoomId !== null) {
            connection.invoke('JoinRoom', activeRoomId)
              .catch((err) => console.error('JoinRoom error:', err));
          }
        })
        .catch((err) => {
          console.error('SignalR Connection Error: ', err);
          message.error('Không thể kết nối đến máy chủ thời gian thực.');
        });

      // Lắng nghe tin nhắn mới (User hoặc AI placeholder)
      connection.on('ReceiveMessage', (newMessage: Message) => {
        // Tránh trùng lặp tin nhắn nếu tải lịch sử có độ trễ
        setMessages((prev) => {
          if (prev.some(m => m.id === newMessage.id)) return prev;
          return [...prev, newMessage];
        });
      });

      // Lắng nghe stream chữ từ AI Copilot
      connection.on('ReceiveAiToken', (tempId: number, tokenStr: string) => {
        setMessages((prev) =>
          prev.map((msg) =>
            msg.id === tempId
              ? { ...msg, content: msg.content + tokenStr }
              : msg
          )
        );
      });

      // Lắng nghe thông báo hoàn thành stream AI
      connection.on('ReceiveAiComplete', (tempId: number, finalId: number, finalContent: string) => {
        setMessages((prev) =>
          prev.map((msg) =>
            msg.id === tempId
              ? { ...msg, id: finalId, content: finalContent }
              : msg
          )
        );
      });

      // Lắng nghe thông báo hệ thống (Tham gia/rời phòng)
      connection.on('ReceiveNotification', (notifyText: string) => {
        setNotifications((prev) => [...prev, notifyText]);
        // Tự động xóa thông báo sau 5 giây
        setTimeout(() => {
          setNotifications((prev) => prev.filter((n) => n !== notifyText));
        }, 5000);
      });

      // Cập nhật danh sách user đang online trong phòng
      connection.on('PresenceUpdate', (roomId: number, usernames: string[]) => {
        if (roomId === activeRoomId) {
          setOnlineUsers(usernames);
        }
      });

      // Cập nhật danh sách user đang gõ
      connection.on('TypingUpdate', (roomId: number, user: string, isTyping: boolean) => {
        if (roomId !== activeRoomId || user === username) return;
        setTypingUsers((prev) => {
          if (isTyping) {
            return prev.includes(user) ? prev : [...prev, user];
          }
          return prev.filter((u) => u !== user);
        });
      });

      return () => {
        connection.off('ReceiveMessage');
        connection.off('ReceiveAiToken');
        connection.off('ReceiveAiComplete');
        connection.off('ReceiveNotification');
        connection.off('PresenceUpdate');
        connection.off('TypingUpdate');
        if (connection.state === 'Connected' && activeRoomId !== null) {
          connection.invoke('LeaveRoom', activeRoomId)
            .catch((err) => console.error('LeaveRoom error:', err));
        }
        connection.stop();
      };
    }
  }, [connection, activeRoomId]);

  // Tự động cuộn xuống dưới khi có tin nhắn mới
  useEffect(() => {
    messageEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const sendTyping = (isTyping: boolean) => {
    const conn = connectionRef.current;
    const roomId = activeRoomIdRef.current;
    if (!conn || conn.state !== 'Connected' || roomId === null) return;
    conn.invoke('Typing', roomId, isTyping).catch(() => {});
  };

  const handleInputChange = (value: string) => {
    setInputText(value);
    if (!isTypingRef.current) {
      isTypingRef.current = true;
      sendTyping(true);
    }
    if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
    typingTimeoutRef.current = setTimeout(() => {
      isTypingRef.current = false;
      sendTyping(false);
    }, 2000);
  };

  // 5. Gửi tin nhắn qua SignalR Hub
  const handleSendMessage = async (textToSend?: string) => {
    const text = textToSend !== undefined ? textToSend : inputText;
    if (!text.trim() || activeRoomId === null || !connection) return;

    try {
      await connection.invoke('SendMessage', activeRoomId, text.trim());
      if (isTypingRef.current) {
        if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
        isTypingRef.current = false;
        sendTyping(false);
      }
      if (textToSend === undefined) {
        setInputText('');
      }
    } catch (err) {
      message.error('Không thể gửi tin nhắn.');
    }
  };

  const handleRoomCreated = (newRoom: Room) => {
    setRooms((prev) => [...prev, newRoom]);
    setActiveRoomId(newRoom.id);
  };

  const activeRoom = rooms.find((r) => r.id === activeRoomId);

  return (
    <div style={{
      display: 'flex',
      height: '100vh',
      width: '100vw',
      backgroundColor: '#0a0f1d',
      overflow: 'hidden'
    }}>
      {/* 1. Sidebar bên trái: Danh sách phòng */}
      <RoomSidebar
        rooms={rooms}
        activeRoomId={activeRoomId}
        onSelectRoom={setActiveRoomId}
        onRoomCreated={handleRoomCreated}
        onLogout={onLogout}
        username={username}
      />

      {/* 2. Khung chat ở giữa */}
      <div style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        backgroundColor: '#0a0f1d',
        position: 'relative'
      }}>
        {/* Room Header */}
        <div style={{
          padding: '16px 20px',
          borderBottom: '1px solid #1e293b',
          backgroundColor: '#0f172a',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <div>
            <span style={{ color: '#a3e635', fontWeight: 'bold', fontSize: '15px', fontFamily: 'monospace' }}>
              #{activeRoom ? activeRoom.name : 'NO_ROOM_SELECTED'}
            </span>
            <div style={{ fontSize: '11px', color: '#64748b', fontFamily: 'monospace', marginTop: '2px' }}>
              {activeRoom ? activeRoom.description : 'Please select a room to start discussions.'}
            </div>
            {activeRoom && (
              <div
                style={{ fontSize: '11px', color: '#a3e635', fontFamily: 'monospace', marginTop: '2px' }}
                title={onlineUsers.join(', ')}
              >
                ● {onlineUsers.length} online{onlineUsers.length > 0 ? `: ${onlineUsers.join(', ')}` : ''}
              </div>
            )}
          </div>
        </div>

        {/* Floating Notification Banner */}
        <div style={{
          position: 'absolute',
          top: '70px',
          left: '50%',
          transform: 'translateX(-50%)',
          zIndex: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: '4px',
          pointerEvents: 'none'
        }}>
          {notifications.map((notif, idx) => (
            <div
              key={idx}
              style={{
                backgroundColor: '#1e293bca',
                border: '1px solid #a3e63550',
                color: '#a3e635',
                padding: '6px 16px',
                fontSize: '11px',
                fontFamily: 'monospace',
                boxShadow: '0 4px 10px rgba(0,0,0,0.3)'
              }}
            >
              {notif}
            </div>
          ))}
        </div>

        {/* Message Area */}
        <div style={{
          flex: 1,
          overflowY: 'auto',
          padding: '20px',
          display: 'flex',
          flexDirection: 'column',
          gap: '16px'
        }}>
          {messages.length === 0 ? (
            <div style={{
              display: 'flex',
              justifyContent: 'center',
              alignItems: 'center',
              height: '100%',
              color: '#475569',
              fontFamily: 'monospace',
              fontSize: '12px'
            }}>
              // NO_MESSAGES_YET - START_THE_CONVERSATION
            </div>
          ) : (
            messages.map((msg) => {
              const isMe = msg.senderName === username;
              const isAi = msg.isAi;
              return (
                <div
                  key={msg.id}
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignSelf: isMe ? 'flex-end' : 'flex-start',
                    maxWidth: '70%',
                    alignItems: isMe ? 'flex-end' : 'flex-start'
                  }}
                >
                  {/* Sender Name and Time */}
                  <div style={{
                    fontSize: '10px',
                    color: isAi ? '#a3e635' : '#64748b',
                    fontFamily: 'monospace',
                    marginBottom: '4px',
                    display: 'flex',
                    gap: '8px'
                  }}>
                    <span>{msg.senderName}</span>
                    <span>[{new Date(msg.createdAt).toLocaleTimeString()}]</span>
                  </div>

                  {/* Message Bubble */}
                  <div style={{
                    padding: '10px 14px',
                    backgroundColor: isMe ? '#a3e635' : isAi ? '#0f172a' : '#1e293b',
                    color: isMe ? '#000' : '#f8fafc',
                    border: isAi ? '1px solid #a3e635' : '1px solid transparent',
                    borderRadius: 0,
                    fontSize: '13px',
                    lineHeight: '1.5',
                    fontFamily: isAi ? 'monospace' : 'sans-serif',
                    whiteSpace: 'pre-wrap',
                    boxShadow: isAi ? '0 0 8px rgba(163, 230, 53, 0.1)' : 'none'
                  }}>
                    {msg.content === '' && isAi ? (
                      <span className="copilot-loading">Copilot đang phản hồi...</span>
                    ) : (
                      msg.content
                    )}
                  </div>
                </div>
              );
            })
          )}
          <div ref={messageEndRef} />
        </div>

        {/* Typing Indicator */}
        {typingUsers.length > 0 && (
          <div style={{
            padding: '4px 20px',
            fontSize: '11px',
            color: '#a3e635',
            fontFamily: 'monospace',
            fontStyle: 'italic'
          }}>
            {typingUsers.join(', ')} đang gõ...
          </div>
        )}

        {/* Input Message Area */}
        <div style={{
          padding: '16px 20px',
          borderTop: '1px solid #1e293b',
          backgroundColor: '#0f172a',
          display: 'flex',
          gap: '10px'
        }}>
          <Input
            value={inputText}
            onChange={(e) => handleInputChange(e.target.value)}
            onPressEnter={() => handleSendMessage()}
            placeholder="Type a message... (Use @copilot to query AI Assistant)"
            style={{
              backgroundColor: '#1e293b',
              borderColor: '#475569',
              color: '#fff',
              borderRadius: 0,
              fontFamily: 'monospace'
            }}
          />
          <Button
            type="primary"
            icon={<SendOutlined />}
            onClick={() => handleSendMessage()}
            style={{
              backgroundColor: '#a3e635',
              borderColor: '#a3e635',
              color: '#000',
              borderRadius: 0,
              fontWeight: 'bold',
              fontFamily: 'monospace'
            }}
          >
            SEND
          </Button>
        </div>
      </div>

      {/* 3. Panel bên phải: Trợ lý AI */}
      <CopilotPanel onTriggerCommand={handleSendMessage} />
    </div>
  );
};
