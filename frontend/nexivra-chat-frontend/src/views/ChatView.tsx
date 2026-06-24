/* eslint-disable react-hooks/set-state-in-effect */
/* eslint-disable react-hooks/exhaustive-deps */
/* eslint-disable @typescript-eslint/no-unused-vars */
import React, { useState, useEffect, useRef } from 'react';
import { Input, Button, message } from 'antd';
import { SendOutlined, RobotOutlined } from '@ant-design/icons';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import api, { API_BASE_URL } from '../services/api';
import { RoomSidebar, type Room } from '../components/RoomSidebar';
import { CopilotPanel } from '../components/CopilotPanel';
import { ThemeToggle } from '../components/ThemeToggle';

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
  // Phòng đang tham gia trên hub, để Leave đúng phòng cũ khi chuyển phòng
  const prevRoomRef = useRef<number | null>(null);

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

  // Effect C: Đổi phòng — tải lịch sử, reset UI, và Leave/Join trên hub.
  // KHÔNG khởi động lại kết nối, chỉ chuyển phòng. Idempotent: nếu phòng đã
  // được join sẵn (ví dụ ngay sau khi start() resolve) thì không join lại.
  useEffect(() => {
    if (activeRoomId === null) return;

    setOnlineUsers([]);
    setTypingUsers([]);
    // Clear any in-flight typing timer so it cannot fire against the stale room
    if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
    isTypingRef.current = false;
    fetchMessageHistory(activeRoomId);

    if (connection && connection.state === 'Connected') {
      const prev = prevRoomRef.current;
      if (prev !== activeRoomId) {
        if (prev !== null) {
          connection.invoke('LeaveRoom', prev)
            .catch((err) => console.error('LeaveRoom error:', err));
        }
        connection.invoke('JoinRoom', activeRoomId)
          .catch((err) => console.error('JoinRoom error:', err));
        prevRoomRef.current = activeRoomId;
      }
    }
  }, [activeRoomId, connection]);

  // Effect A+B: Vòng đời kết nối SignalR (build + start + listeners + stop)
  // gói trong MỘT effect theo [token]. Mỗi lần effect chạy tạo một connection
  // riêng; cleanup dừng đúng connection đó. Nhờ vậy StrictMode (mount đôi ở dev)
  // không gây race "start before stop"/"not in Disconnected state" — vì hai lần
  // chạy thao tác trên hai object khác nhau. Listeners đăng ký MỘT lần và đọc
  // activeRoomIdRef để không bị stale khi đổi phòng.
  useEffect(() => {
    const hubUrl = `${API_BASE_URL.replace('/api', '')}/chatHub?access_token=${token}`;
    const conn = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    setConnection(conn);
    connectionRef.current = conn;
    let cancelled = false;

    // Lắng nghe tin nhắn mới (User hoặc AI placeholder)
    conn.on('ReceiveMessage', (newMessage: Message) => {
      // Tránh trùng lặp tin nhắn nếu tải lịch sử có độ trễ
      setMessages((prev) => {
        if (prev.some(m => m.id === newMessage.id)) return prev;
        return [...prev, newMessage];
      });
    });

    // Lắng nghe stream chữ từ AI Copilot
    conn.on('ReceiveAiToken', (tempId: number, tokenStr: string) => {
      setMessages((prev) =>
        prev.map((msg) =>
          msg.id === tempId
            ? { ...msg, content: msg.content + tokenStr }
            : msg
        )
      );
    });

    // Lắng nghe thông báo hoàn thành stream AI
    conn.on('ReceiveAiComplete', (tempId: number, finalId: number, finalContent: string) => {
      setMessages((prev) =>
        prev.map((msg) =>
          msg.id === tempId
            ? { ...msg, id: finalId, content: finalContent }
            : msg
        )
      );
    });

    // Lắng nghe thông báo hệ thống (Tham gia/rời phòng)
    conn.on('ReceiveNotification', (notifyText: string) => {
      setNotifications((prev) => [...prev, notifyText]);
      // Tự động xóa thông báo sau 5 giây
      setTimeout(() => {
        setNotifications((prev) => prev.filter((n) => n !== notifyText));
      }, 5000);
    });

    // Cập nhật danh sách user đang online trong phòng (đọc ref để không stale)
    conn.on('PresenceUpdate', (roomId: number, usernames: string[]) => {
      if (roomId === activeRoomIdRef.current) {
        setOnlineUsers(usernames);
      }
    });

    // Cập nhật danh sách user đang gõ (đọc ref để không stale)
    conn.on('TypingUpdate', (roomId: number, user: string, isTyping: boolean) => {
      if (roomId !== activeRoomIdRef.current || user === username) return;
      setTypingUsers((prev) => {
        if (isTyping) {
          return prev.includes(user) ? prev : [...prev, user];
        }
        return prev.filter((u) => u !== user);
      });
    });

    conn.start()
      .then(() => {
        if (cancelled) return;
        console.log('SignalR connected.');
        const roomId = activeRoomIdRef.current;
        if (roomId !== null) {
          conn.invoke('JoinRoom', roomId)
            .catch((err) => console.error('JoinRoom error:', err));
          prevRoomRef.current = roomId;
        }
      })
      .catch((err) => {
        if (cancelled) return;
        console.error('SignalR Connection Error: ', err);
        message.error('Không thể kết nối đến máy chủ thời gian thực.');
      });

    return () => {
      cancelled = true;
      conn.off('ReceiveMessage');
      conn.off('ReceiveAiToken');
      conn.off('ReceiveAiComplete');
      conn.off('ReceiveNotification');
      conn.off('PresenceUpdate');
      conn.off('TypingUpdate');
      prevRoomRef.current = null;
      // stop() sẽ hủy start() đang chờ (nếu có); lỗi abort được nuốt qua cờ cancelled
      conn.stop().catch(() => {});
    };
  }, [token]);

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
    <div style={{ display: 'flex', height: '100vh', width: '100vw', backgroundColor: 'var(--bg-canvas)', overflow: 'hidden' }}>
      <RoomSidebar
        rooms={rooms}
        activeRoomId={activeRoomId}
        onSelectRoom={setActiveRoomId}
        onRoomCreated={handleRoomCreated}
        onLogout={onLogout}
        username={username}
      />

      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', backgroundColor: 'var(--bg-surface)', position: 'relative' }}>
        {/* Room Header */}
        <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--border)', backgroundColor: 'var(--bg-surface)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <span style={{ color: 'var(--text-primary)', fontWeight: 600, fontSize: '16px', fontFamily: "'Outfit', sans-serif" }}>
              {activeRoom ? `# ${activeRoom.name}` : 'Chưa chọn phòng'}
            </span>
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '2px' }}>
              {activeRoom ? activeRoom.description : 'Chọn một phòng để bắt đầu trò chuyện'}
            </div>
            {activeRoom && (
              <div style={{ fontSize: '12px', color: 'var(--primary)', marginTop: '3px', display: 'flex', alignItems: 'center', gap: '5px' }} title={onlineUsers.join(', ')}>
                <span style={{ width: 7, height: 7, borderRadius: '50%', background: 'var(--primary)' }} />
                {onlineUsers.length} người đang online{onlineUsers.length > 0 ? `: ${onlineUsers.join(', ')}` : ''}
              </div>
            )}
          </div>
          <ThemeToggle />
        </div>

        {/* Floating Notification Banner */}
        <div style={{ position: 'absolute', top: '70px', left: '50%', transform: 'translateX(-50%)', zIndex: 10, display: 'flex', flexDirection: 'column', gap: '4px', pointerEvents: 'none' }}>
          {notifications.map((notif, idx) => (
            <div key={idx} style={{ backgroundColor: 'var(--bg-elevated)', border: '1px solid var(--border)', color: 'var(--text-secondary)', padding: '6px 16px', fontSize: '12px', borderRadius: 8, boxShadow: '0 4px 10px rgba(0,0,0,0.08)' }}>
              {notif}
            </div>
          ))}
        </div>

        {/* Message Area */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '20px', display: 'flex', flexDirection: 'column', gap: '14px', backgroundColor: 'var(--bg-canvas)' }}>
          {messages.length === 0 ? (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%', color: 'var(--text-muted)', fontSize: '14px' }}>
              Chưa có tin nhắn — hãy bắt đầu trò chuyện 👋
            </div>
          ) : (
            messages.map((msg) => {
              const isMe = msg.senderName === username;
              const isAi = msg.isAi;
              return (
                <div key={msg.id} style={{ display: 'flex', flexDirection: 'column', alignSelf: isMe ? 'flex-end' : 'flex-start', maxWidth: '74%', alignItems: isMe ? 'flex-end' : 'flex-start' }}>
                  <div style={{ fontSize: '11px', color: isAi ? 'var(--primary)' : 'var(--text-muted)', marginBottom: '4px', display: 'flex', gap: '6px', alignItems: 'center' }}>
                    {isAi && <RobotOutlined />}
                    <span>{isAi ? 'Trợ lý AI' : msg.senderName}</span>
                    <span>{new Date(msg.createdAt).toLocaleTimeString()}</span>
                  </div>
                  <div style={{
                    padding: '9px 13px',
                    backgroundColor: isMe ? 'var(--bubble-me)' : isAi ? 'var(--bubble-ai-bg)' : 'var(--bubble-other)',
                    color: isMe ? 'var(--bubble-me-text)' : isAi ? 'var(--bubble-ai-text)' : 'var(--bubble-other-text)',
                    border: isAi ? '1px solid var(--bubble-ai-border)' : isMe ? 'none' : '1px solid var(--border)',
                    borderRadius: isMe ? '14px 14px 4px 14px' : '14px 14px 14px 4px',
                    fontSize: '14px',
                    lineHeight: '1.5',
                    whiteSpace: 'pre-wrap',
                  }}>
                    {msg.content === '' && isAi ? (
                      <span className="copilot-loading">Trợ lý AI đang phản hồi…</span>
                    ) : (
                      msg.content
                    )}
                  </div>
                </div>
              );
            })
          )}
          {typingUsers.length > 0 && (
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontStyle: 'italic' }}>
              {typingUsers.join(', ')} đang gõ…
            </div>
          )}
          <div ref={messageEndRef} />
        </div>

        {/* Input Area */}
        <div style={{ padding: '14px 20px', borderTop: '1px solid var(--border)', backgroundColor: 'var(--bg-surface)', display: 'flex', gap: '10px' }}>
          <Input
            value={inputText}
            onChange={(e) => handleInputChange(e.target.value)}
            onPressEnter={() => handleSendMessage()}
            placeholder="Nhập tin nhắn… (gõ @copilot để hỏi AI)"
          />
          <Button type="primary" icon={<SendOutlined />} onClick={() => handleSendMessage()} style={{ fontWeight: 500 }}>
            Gửi
          </Button>
        </div>
      </div>

      <CopilotPanel onTriggerCommand={handleSendMessage} />
    </div>
  );
};
