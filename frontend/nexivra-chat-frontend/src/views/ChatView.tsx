/* eslint-disable react-hooks/set-state-in-effect */
/* eslint-disable react-hooks/exhaustive-deps */
/* eslint-disable @typescript-eslint/no-unused-vars */
import React, { useState, useEffect, useRef, useCallback, lazy, Suspense } from 'react';
import { flushSync } from 'react-dom';
import { Input, Button, message } from 'antd';
import { SendOutlined } from '@ant-design/icons';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import api, { API_BASE_URL } from '../services/api';
import { RoomSidebar, type Room, type SidebarUser } from '../components/RoomSidebar';
import { CopilotPanel } from '../components/CopilotPanel';
import { ThemeToggle } from '../components/ThemeToggle';
import { MessageBubble } from '../components/MessageBubble';

const ProfileView = lazy(() =>
  import('./ProfileView').then((m) => ({ default: m.ProfileView }))
);

export interface Message {
  id: number;
  roomId?: number;
  privateChatId?: number;
  senderId?: number;
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
  const [users, setUsers] = useState<SidebarUser[]>([]);
  const [activeRoomId, setActiveRoomId] = useState<number | null>(null);
  const [activePrivateChatId, setActivePrivateChatId] = useState<number | null>(null);
  const [activeChatType, setActiveChatType] = useState<'room' | 'private'>('room');
  const [activeRecipient, setActiveRecipient] = useState<SidebarUser | null>(null);
  
  const [messages, setMessages] = useState<Message[]>([]);
  const [inputText, setInputText] = useState('');
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [notifications, setNotifications] = useState<string[]>([]);
  const [onlineUsers, setOnlineUsers] = useState<string[]>([]);
  const [typingUsers, setTypingUsers] = useState<string[]>([]);
  const [translations, setTranslations] = useState<Record<number, string>>({});
  const [translatingIds, setTranslatingIds] = useState<Record<number, boolean>>({});
  // Số tin chưa đọc theo phòng / theo DM. Key là id (number; object JS coerce "1"===1 nên
  // dữ liệu JSON từ backend với key string vẫn tra cứu được bằng number).
  const [unread, setUnread] = useState<{ rooms: Record<number, number>; privateChats: Record<number, number> }>({ rooms: {}, privateChats: {} });
  const [profileUserId, setProfileUserId] = useState<number | null>(null);
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  // Chỉ mount ProfileView (lazy chunk) sau lần đầu mở hồ sơ; giữ mount để Modal có animation đóng/mở.
  const [profileEverOpened, setProfileEverOpened] = useState(false);
  const [partnerLastReadId, setPartnerLastReadId] = useState<number>(0);

  const [hasMore, setHasMore] = useState(true);
  const [loadingOlder, setLoadingOlder] = useState(false);

  // Giữ bản tham chiếu users mới nhất để callback mở-profile-theo-tên ổn định
  // (không tạo lại khi presence cập nhật), nhờ đó React.memo của MessageBubble giữ hiệu lực.
  const usersRef = useRef(users);
  useEffect(() => { usersRef.current = users; }, [users]);

  // Đánh dấu một hội thoại đã đọc.
  // - serverId: room → roomId; private → privateChatId (khóa lưu trữ).
  // - badgeKey: room → roomId; private → id NGƯỜI ĐỐI THOẠI (khóa badge ở sidebar).
  //   Bỏ qua xóa badge nếu badgeKey không truyền (vd hội thoại đang mở, badge vốn = 0).
  const markConversationRead = useCallback((type: 'room' | 'private', serverId: number | null, lastMessageId: number, badgeKey?: number) => {
    if (!serverId) return;
    if (badgeKey !== undefined && badgeKey !== null) {
      setUnread((prev) => {
        const key = type === 'room' ? 'rooms' : 'privateChats';
        if (prev[key][badgeKey] === undefined) return prev;
        const inner = { ...prev[key] };
        delete inner[badgeKey];
        return { ...prev, [key]: inner };
      });
    }
    const conn = connectionRef.current;
    if (!conn || conn.state !== 'Connected') return;
    const lastId = lastMessageId && lastMessageId > 0 ? lastMessageId : 0;
    if (type === 'room') conn.invoke('MarkRead', serverId, null, lastId).catch(() => {});
    else conn.invoke('MarkRead', null, serverId, lastId).catch(() => {});
  }, []);

  const fetchUnreadCounts = useCallback(async () => {
    try {
      const response = await api.get('/users/unread-counts');
      setUnread({
        rooms: response.data.rooms || {},
        privateChats: response.data.privateChats || {},
      });
    } catch (err) {
      console.error('Không thể lấy số tin chưa đọc.', err);
    }
  }, []);

  const handleOpenProfile = useCallback((userId: number) => {
    setProfileUserId(userId);
    setIsProfileOpen(true);
    setProfileEverOpened(true);
  }, []);

  // Mở hồ sơ theo tên người gửi (dùng cho click vào tên trong bong bóng).
  // Đọc usersRef.current để callback ổn định, không phụ thuộc state users.
  const handleOpenSenderProfile = useCallback((senderName: string) => {
    const targetUser = usersRef.current.find((u) => u.username === senderName);
    if (targetUser) {
      handleOpenProfile(targetUser.id);
    } else if (senderName === username) {
      handleOpenProfile(0);
    }
  }, [handleOpenProfile, username]);

  const typingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isTypingRef = useRef(false);
  
  const connectionRef = useRef<HubConnection | null>(null);
  const activeRoomIdRef = useRef<number | null>(null);
  const activePrivateChatIdRef = useRef<number | null>(null);
  const activeChatTypeRef = useRef<'room' | 'private'>('room');
  // id người đối thoại của DM đang mở (khóa badge keyed-by-user).
  const activeRecipientIdRef = useRef<number | null>(null);
  // Phòng đang tham gia trên hub, để Leave đúng phòng cũ khi chuyển phòng
  const prevRoomRef = useRef<number | null>(null);
  const lastReceivedMessageIdRef = useRef<number | null>(null);

  const messageEndRef = useRef<HTMLDivElement>(null);
  const prevMessageCountRef = useRef(0);

  const messagesContainerRef = useRef<HTMLDivElement>(null);
  const sentinelRef = useRef<HTMLDivElement>(null);
  const isPrependingRef = useRef(false);

  const oldestMessageId = (() => {
    const positiveIds = messages.filter(m => m.id > 0).map(m => m.id);
    return positiveIds.length > 0 ? Math.min(...positiveIds) : null;
  })();

  // 1. Tải danh sách phòng từ API
  const fetchRooms = async () => {
    try {
      const response = await api.get('/rooms');
      const roomsData = response.data;
      setRooms(roomsData);
      if (roomsData.length > 0 && activeRoomId === null && activeChatType === 'room') {
        setActiveRoomId(roomsData[0].id);
      }
    } catch (err) {
      message.error('Không thể lấy danh sách phòng chat.');
    }
  };

  // 2. Tải danh sách người dùng từ API
  const fetchUsers = async () => {
    try {
      const response = await api.get('/users');
      setUsers(response.data.map((u: any) => ({ ...u, isOnline: false })));
    } catch (err) {
      console.error('Không thể lấy danh sách người dùng.', err);
    }
  };

  useEffect(() => {
    fetchRooms();
    fetchUsers();
    fetchUnreadCounts();
  }, []);

  // 3. Tải lịch sử tin nhắn phòng chat (mở phòng = đã đọc tới tin cuối)
  const fetchMessageHistory = async (roomId: number) => {
    try {
      setHasMore(true);
      setLoadingOlder(false);
      isPrependingRef.current = false;
      setPartnerLastReadId(0);
      const response = await api.get(`/rooms/${roomId}/messages?limit=50`);
      const data = response.data as Message[];
      setMessages(data);
      setHasMore(data.length >= 50);
      const lastId = data.length ? data[data.length - 1].id : 0;
      markConversationRead('room', roomId, lastId, roomId);
    } catch (err) {
      message.error('Không thể lấy lịch sử tin nhắn.');
    }
  };

  // 4. Tải lịch sử tin nhắn riêng tư (mở DM = đã đọc tới tin cuối).
  //    recipientUserId là khóa badge ở sidebar (DM key theo người đối thoại).
  const fetchPrivateMessageHistory = async (chatId: number, recipientUserId?: number) => {
    try {
      setHasMore(true);
      setLoadingOlder(false);
      isPrependingRef.current = false;
      const response = await api.get(`/users/private-chat/${chatId}/messages?limit=50`);
      const data = response.data as Message[];
      setMessages(data);
      setHasMore(data.length >= 50);

      const partnerReadHeader = response.headers['x-partner-last-read-id'];
      if (partnerReadHeader) {
        setPartnerLastReadId(parseInt(partnerReadHeader, 10) || 0);
      } else {
        setPartnerLastReadId(0);
      }

      const lastId = data.length ? data[data.length - 1].id : 0;
      markConversationRead('private', chatId, lastId, recipientUserId);
    } catch (err) {
      message.error('Không thể lấy lịch sử tin nhắn riêng tư.');
    }
  };

  const loadOlderMessages = async () => {
    if (loadingOlder || !hasMore || !oldestMessageId) return;

    setLoadingOlder(true);
    isPrependingRef.current = true;

    const container = messagesContainerRef.current;
    const prevScrollHeight = container ? container.scrollHeight : 0;
    const prevScrollTop = container ? container.scrollTop : 0;

    try {
      let response;
      if (activeChatType === 'room') {
        if (activeRoomId === null) return;
        response = await api.get(`/rooms/${activeRoomId}/messages?limit=50&beforeId=${oldestMessageId}`);
      } else {
        if (activePrivateChatId === null) return;
        response = await api.get(`/users/private-chat/${activePrivateChatId}/messages?limit=50&beforeId=${oldestMessageId}`);
      }

      const newMessages = response.data as Message[];

      flushSync(() => {
        setMessages((prev) => {
          const merged = [...newMessages, ...prev];
          const unique: Message[] = [];
          const seen = new Set<number>();
          for (const m of merged) {
            if (!seen.has(m.id)) {
              seen.add(m.id);
              unique.push(m);
            }
          }
          return unique;
        });
      });

      if (container) {
        const newScrollHeight = container.scrollHeight;
        container.scrollTop = prevScrollTop + (newScrollHeight - prevScrollHeight);
      }

      if (newMessages.length < 50) {
        setHasMore(false);
      }
    } catch (err) {
      message.error('Không thể tải tin nhắn cũ hơn.');
    } finally {
      setLoadingOlder(false);
      isPrependingRef.current = false;
    }
  };

  useEffect(() => { connectionRef.current = connection; }, [connection]);
  useEffect(() => { activeRoomIdRef.current = activeRoomId; }, [activeRoomId]);
  useEffect(() => { activePrivateChatIdRef.current = activePrivateChatId; }, [activePrivateChatId]);
  useEffect(() => { activeChatTypeRef.current = activeChatType; }, [activeChatType]);
  useEffect(() => { activeRecipientIdRef.current = activeRecipient?.id ?? null; }, [activeRecipient]);

  useEffect(() => {
    const positiveIds = messages.filter(m => m.id > 0).map(m => m.id);
    lastReceivedMessageIdRef.current = positiveIds.length > 0 ? Math.max(...positiveIds) : null;
  }, [messages]);

  // Effect C: Đổi phòng/hội thoại — tải lịch sử, reset UI, và Leave/Join trên hub.
  useEffect(() => {
    setOnlineUsers([]);
    setTypingUsers([]);
    setTranslations({});
    setTranslatingIds({});
    if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
    isTypingRef.current = false;

    if (activeChatType === 'room' && activeRoomId !== null) {
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
    } else if (activeChatType === 'private' && activePrivateChatId !== null) {
      fetchPrivateMessageHistory(activePrivateChatId, activeRecipient?.id);

      if (connection && connection.state === 'Connected') {
        const prev = prevRoomRef.current;
        if (prev !== null) {
          connection.invoke('LeaveRoom', prev)
            .catch((err) => console.error('LeaveRoom error:', err));
          prevRoomRef.current = null;
        }
      }
    }
  }, [activeChatType, activeRoomId, activePrivateChatId, connection]);

  // Effect A+B: Vòng đời kết nối SignalR
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

    // Lắng nghe tin nhắn mới phòng chat
    conn.on('ReceiveMessage', (newMessage: Message) => {
      if (activeChatTypeRef.current === 'room' && newMessage.roomId === activeRoomIdRef.current) {
        setMessages((prev) => {
          if (prev.some(m => m.id === newMessage.id)) return prev;
          return [...prev, newMessage];
        });
        // Tin tới phòng đang mở → đánh dấu đã đọc (badge giữ 0, advance last_read).
        if (newMessage.id > 0 && newMessage.roomId) {
          markConversationRead('room', newMessage.roomId, newMessage.id, newMessage.roomId);
        }
      }
    });

    // Lắng nghe tin nhắn riêng tư mới
    conn.on('ReceivePrivateMessage', (newMessage: Message) => {
      if (activeChatTypeRef.current === 'private' && newMessage.privateChatId === activePrivateChatIdRef.current) {
        setMessages((prev) => {
          if (prev.some(m => m.id === newMessage.id)) return prev;
          return [...prev, newMessage];
        });
        if (newMessage.id > 0 && newMessage.privateChatId) {
          markConversationRead('private', newMessage.privateChatId, newMessage.id);
        }
      }
    });

    // Tín hiệu unread: tăng badge nếu hội thoại KHÔNG đang mở.
    conn.on('UnreadUpdate', (payload: { type: 'room' | 'private'; id: number }) => {
      const { type, id } = payload;
      const isActive = type === 'room'
        ? (activeChatTypeRef.current === 'room' && id === activeRoomIdRef.current)
        : (activeChatTypeRef.current === 'private' && id === activeRecipientIdRef.current);
      if (isActive) return;
      setUnread((prev) => {
        const key = type === 'room' ? 'rooms' : 'privateChats';
        const cur = prev[key][id] || 0;
        return { ...prev, [key]: { ...prev[key], [id]: cur + 1 } };
      });
    });

    // Đồng bộ "đã đọc" giữa các tab của chính user.
    conn.on('ReadUpdate', (payload: { roomId: number | null; privateChatUserId: number | null }) => {
      setUnread((prev) => {
        if (payload.roomId) {
          const inner = { ...prev.rooms }; delete inner[payload.roomId];
          return { ...prev, rooms: inner };
        }
        if (payload.privateChatUserId) {
          const inner = { ...prev.privateChats }; delete inner[payload.privateChatUserId];
          return { ...prev, privateChats: inner };
        }
        return prev;
      });
    });

    // Lắng nghe trạng thái "Đã xem" từ đối phương trong DM.
    conn.on('SeenUpdate', (payload: { privateChatUserId: number; lastReadMessageId: number }) => {
      if (activeChatTypeRef.current === 'private' && activeRecipientIdRef.current === payload.privateChatUserId) {
        setPartnerLastReadId(payload.lastReadMessageId);
      }
    });

    // Lắng nghe stream chữ từ AI Copilot
    conn.on('ReceiveAiToken', (tempId: number, tokenStr: string) => {
      if (activeChatTypeRef.current === 'room') {
        setMessages((prev) =>
          prev.map((msg) =>
            msg.id === tempId
              ? { ...msg, content: msg.content + tokenStr }
              : msg
          )
        );
      }
    });

    // Lắng nghe thông báo hoàn thành stream AI
    conn.on('ReceiveAiComplete', (tempId: number, finalId: number, finalContent: string) => {
      if (activeChatTypeRef.current === 'room') {
        setMessages((prev) =>
          prev.map((msg) =>
            msg.id === tempId
              ? { ...msg, id: finalId, content: finalContent }
              : msg
          )
        );
      }
    });

    // Lắng nghe thông báo hệ thống (Tham gia/rời phòng)
    conn.on('ReceiveNotification', (notifyText: string) => {
      setNotifications((prev) => [...prev, notifyText]);
      setTimeout(() => {
        setNotifications((prev) => prev.filter((n) => n !== notifyText));
      }, 5000);
    });

    // Cập nhật danh sách user online trong phòng
    conn.on('PresenceUpdate', (roomId: number, usernames: string[]) => {
      if (activeChatTypeRef.current === 'room' && roomId === activeRoomIdRef.current) {
        setOnlineUsers(usernames);
      }
    });

    // Cập nhật danh sách online toàn cục
    conn.on('GlobalPresenceUpdate', (onlineUsernames: string[]) => {
      setUsers((prevUsers) =>
        prevUsers.map((u) => ({
          ...u,
          isOnline: onlineUsernames.includes(u.username),
        }))
      );
    });

    // Cập nhật danh sách user đang gõ
    conn.on('TypingUpdate', (roomId: number, user: string, isTyping: boolean) => {
      if (activeChatTypeRef.current !== 'room' || roomId !== activeRoomIdRef.current || user === username) return;
      setTypingUsers((prev) => {
        if (isTyping) {
          return prev.includes(user) ? prev : [...prev, user];
        }
        return prev.filter((u) => u !== user);
      });
    });

    // Sau khi kết nối lại: rejoin phòng + refetch tin hội thoại đang mở + refetch unread
    // (fold resync tối thiểu — tin đến lúc mất mạng không bị bỏ lỡ trên badge).
    conn.onreconnected(async () => {
      fetchUnreadCounts();
      const lastId = lastReceivedMessageIdRef.current;

      if (activeChatTypeRef.current === 'room' && activeRoomIdRef.current !== null) {
        const roomId = activeRoomIdRef.current;
        conn.invoke('JoinRoom', roomId).catch(() => {});
        prevRoomRef.current = roomId;

        if (lastId !== null) {
          try {
            let currentAfterId = lastId;
            let keepFetching = true;
            let safetyGuard = 0;
            const accumulated: Message[] = [];

            while (keepFetching && safetyGuard < 20) {
              safetyGuard++;
              const response = await api.get(`/rooms/${roomId}/messages?limit=50&afterId=${currentAfterId}`);
              const batch = response.data as Message[];
              if (!batch || batch.length === 0) break;
              accumulated.push(...batch);
              const maxBatchId = Math.max(...batch.map((m) => m.id));
              currentAfterId = maxBatchId;
              if (batch.length < 50) {
                keepFetching = false;
              }
            }

            if (accumulated.length > 0) {
              flushSync(() => {
                setMessages((prev) => {
                  const merged = [...prev, ...accumulated];
                  const seen = new Set<number>();
                  const unique: Message[] = [];
                  for (const m of merged) {
                    if (!seen.has(m.id)) {
                      seen.add(m.id);
                      unique.push(m);
                    }
                  }
                  return unique;
                });
              });
            }
          } catch (err) {
            console.error('Lỗi khi lặp tải tin nhắn sau reconnect cho phòng, dùng fallback:', err);
            fetchMessageHistory(roomId);
          }
        } else {
          fetchMessageHistory(roomId);
        }
      } else if (activeChatTypeRef.current === 'private' && activePrivateChatIdRef.current !== null) {
        const chatId = activePrivateChatIdRef.current;
        const recipientId = activeRecipientIdRef.current;

        if (lastId !== null) {
          try {
            let currentAfterId = lastId;
            let keepFetching = true;
            let safetyGuard = 0;
            const accumulated: Message[] = [];

            while (keepFetching && safetyGuard < 20) {
              safetyGuard++;
              const response = await api.get(`/users/private-chat/${chatId}/messages?limit=50&afterId=${currentAfterId}`);
              const batch = response.data as Message[];
              if (!batch || batch.length === 0) break;
              accumulated.push(...batch);
              const maxBatchId = Math.max(...batch.map((m) => m.id));
              currentAfterId = maxBatchId;
              if (batch.length < 50) {
                keepFetching = false;
              }
            }

            if (accumulated.length > 0) {
              flushSync(() => {
                setMessages((prev) => {
                  const merged = [...prev, ...accumulated];
                  const seen = new Set<number>();
                  const unique: Message[] = [];
                  for (const m of merged) {
                    if (!seen.has(m.id)) {
                      seen.add(m.id);
                      unique.push(m);
                    }
                  }
                  return unique;
                });
              });
            }
          } catch (err) {
            console.error('Lỗi khi lặp tải tin nhắn sau reconnect cho private-chat, dùng fallback:', err);
            fetchPrivateMessageHistory(chatId, recipientId ?? undefined);
          }
        } else {
          fetchPrivateMessageHistory(chatId, recipientId ?? undefined);
        }
      }
    });

    conn.start()
      .then(() => {
        if (cancelled) return;
        console.log('SignalR connected.');
        
        const roomId = activeRoomIdRef.current;
        if (activeChatTypeRef.current === 'room' && roomId !== null) {
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
      conn.off('ReceivePrivateMessage');
      conn.off('ReceiveAiToken');
      conn.off('ReceiveAiComplete');
      conn.off('ReceiveNotification');
      conn.off('PresenceUpdate');
      conn.off('GlobalPresenceUpdate');
      conn.off('TypingUpdate');
      conn.off('UnreadUpdate');
      conn.off('ReadUpdate');
      conn.off('SeenUpdate');
      prevRoomRef.current = null;
      conn.stop().catch(() => {});
    };
  }, [token]);

  // Tự động cuộn xuống dưới khi có tin nhắn mới.
  // Tin nhắn MỚI (số lượng tăng) -> cuộn mượt. Đang stream token AI
  // (số lượng không đổi, chỉ nội dung bubble cuối cập nhật) -> cuộn tức thì
  // để tránh hiệu ứng smooth chạy liên tục gây giật.
  useEffect(() => {
    const isNewMessage = messages.length !== prevMessageCountRef.current;
    prevMessageCountRef.current = messages.length;
    if (!isPrependingRef.current) {
      messageEndRef.current?.scrollIntoView({ behavior: isNewMessage ? 'smooth' : 'auto' });
    }
  }, [messages]);

  // Thiết lập Intersection Observer cho phân trang cuộn vô hạn lên đầu
  useEffect(() => {
    if (!hasMore || loadingOlder) return;
    const sentinel = sentinelRef.current;
    if (!sentinel) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) {
          loadOlderMessages();
        }
      },
      {
        root: messagesContainerRef.current,
        rootMargin: '100px 0px 0px 0px',
      }
    );

    observer.observe(sentinel);
    return () => {
      observer.disconnect();
    };
  }, [hasMore, loadingOlder, oldestMessageId, activeRoomId, activePrivateChatId, activeChatType]);

  // Tổng unread → tiêu đề tab "(N) Nexivra Chat" để thấy khi tab ở nền.
  useEffect(() => {
    const total =
      Object.values(unread.rooms).reduce((a, b) => a + b, 0) +
      Object.values(unread.privateChats).reduce((a, b) => a + b, 0);
    document.title = total > 0 ? `(${total}) Nexivra Chat` : 'Nexivra Chat';
  }, [unread]);

  const sendTyping = (isTyping: boolean) => {
    const conn = connectionRef.current;
    const roomId = activeRoomIdRef.current;
    if (!conn || conn.state !== 'Connected' || roomId === null || activeChatTypeRef.current !== 'room') return;
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
    if (!text.trim() || !connection) return;

    try {
      if (activeChatType === 'room') {
        if (activeRoomId === null) return;
        await connection.invoke('SendMessage', activeRoomId, text.trim());
      } else {
        if (activePrivateChatId === null || !activeRecipient) return;
        await connection.invoke('SendPrivateMessage', activeRecipient.id, text.trim());
      }

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

  const handleTranslateMessage = useCallback(async (msgId: number, text: string) => {
    const isVietnamese = /[àáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệđìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵ]/i.test(text);
    const targetLanguage = isVietnamese ? 'English' : 'Vietnamese';

    setTranslatingIds(prev => ({ ...prev, [msgId]: true }));
    try {
      const response = await api.post('/translation', { text, targetLanguage });
      setTranslations(prev => ({ ...prev, [msgId]: response.data.translatedText || response.data.TranslatedText }));
    } catch (err) {
      message.error('Không thể dịch tin nhắn.');
    } finally {
      setTranslatingIds(prev => ({ ...prev, [msgId]: false }));
    }
  }, []);

  const handleHideTranslation = useCallback((msgId: number) => {
    setTranslations((prev) => {
      const copy = { ...prev };
      delete copy[msgId];
      return copy;
    });
  }, []);

  const handleSelectRoom = (roomId: number) => {
    setActiveRoomId(roomId);
    setActiveChatType('room');
    setActiveRecipient(null);
    setActivePrivateChatId(null);
  };

  const handleSelectUser = async (userId: number) => {
    try {
      const response = await api.post('/users/private-chat', { receiverId: userId });
      const chat = response.data;
      const selectedUser = users.find(u => u.id === userId);
      if (selectedUser) {
        setActiveRecipient(selectedUser);
      }
      setActivePrivateChatId(chat.id);
      setActiveChatType('private');
      setActiveRoomId(null);
    } catch (err) {
      message.error('Không thể khởi tạo cuộc hội thoại riêng.');
    }
  };

  const handleRoomCreated = (newRoom: Room) => {
    setRooms((prev) => [...prev, newRoom]);
    handleSelectRoom(newRoom.id);
  };

  const activeRoom = rooms.find((r) => r.id === activeRoomId);

  return (
    <div style={{ display: 'flex', height: '100vh', width: '100vw', backgroundColor: 'var(--bg-canvas)', overflow: 'hidden' }}>
      <RoomSidebar
        rooms={rooms}
        users={users}
        activeRoomId={activeRoomId}
        activeChatType={activeChatType}
        onSelectRoom={handleSelectRoom}
        onSelectUser={handleSelectUser}
        onRoomCreated={handleRoomCreated}
        onLogout={onLogout}
        username={username}
        onOpenProfile={handleOpenProfile}
        unreadRooms={unread.rooms}
        unreadPrivateChats={unread.privateChats}
        activePrivateUserId={activeRecipient?.id ?? null}
      />

      <div style={{ flex: 1, minWidth: 0, minHeight: 0, display: 'flex', flexDirection: 'column', height: '100%', backgroundColor: 'var(--bg-surface)', position: 'relative' }}>
        {/* Room Header */}
        <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--border)', backgroundColor: 'var(--bg-surface)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <span 
              style={{ 
                color: 'var(--text-primary)', 
                fontWeight: 600, 
                fontSize: '16px', 
                fontFamily: "'Outfit', sans-serif",
                cursor: activeChatType === 'private' ? 'pointer' : 'default',
                textDecoration: activeChatType === 'private' ? 'underline' : 'none'
              }}
              onClick={() => {
                if (activeChatType === 'private' && activeRecipient) {
                  handleOpenProfile(activeRecipient.id);
                }
              }}
              title={activeChatType === 'private' ? "Xem hồ sơ" : undefined}
            >
              {activeChatType === 'room'
                ? (activeRoom ? `# ${activeRoom.name}` : 'Chưa chọn phòng')
                : (activeRecipient ? `@ ${activeRecipient.username}` : 'Trò chuyện cá nhân')}
            </span>
            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '2px' }}>
              {activeChatType === 'room'
                ? (activeRoom ? activeRoom.description : 'Chọn một phòng để bắt đầu trò chuyện')
                : (activeRecipient ? 'Tin nhắn riêng tư bảo mật' : 'Chọn một người bạn để bắt đầu trò chuyện')}
            </div>
            {activeChatType === 'room' && activeRoom && (
              <div style={{ fontSize: '12px', color: 'var(--primary)', marginTop: '3px', display: 'flex', alignItems: 'center', gap: '5px' }} title={onlineUsers.join(', ')}>
                <span style={{ width: 7, height: 7, borderRadius: '50%', background: 'var(--primary)' }} />
                {onlineUsers.length} người đang online{onlineUsers.length > 0 ? `: ${onlineUsers.join(', ')}` : ''}
              </div>
            )}
            {activeChatType === 'private' && activeRecipient && (
              <div style={{ fontSize: '12px', color: activeRecipient.isOnline ? 'var(--primary)' : 'var(--text-muted)', marginTop: '3px', display: 'flex', alignItems: 'center', gap: '5px' }}>
                <span style={{ width: 7, height: 7, borderRadius: '50%', background: activeRecipient.isOnline ? 'var(--primary)' : 'var(--text-muted)' }} />
                {activeRecipient.isOnline ? 'Đang hoạt động' : 'Ngoại tuyến'}
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
        <div ref={messagesContainerRef} style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '20px', display: 'flex', flexDirection: 'column', gap: '14px', backgroundColor: 'var(--bg-canvas)' }}>
          {messages.length > 0 && (
            <>
              {hasMore && (
                <div ref={sentinelRef} style={{ height: '5px', margin: '-5px 0' }} />
              )}
              {hasMore && (
                <div style={{ display: 'flex', justifyContent: 'center', padding: '10px 0' }}>
                  <Button type="link" loading={loadingOlder} onClick={loadOlderMessages} style={{ color: 'var(--primary)' }}>
                    Tải tin nhắn cũ hơn
                  </Button>
                </div>
              )}
              {!hasMore && (
                <div style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '10px 0', fontSize: '12px' }}>
                  Đầu hội thoại
                </div>
              )}
            </>
          )}

          {messages.length === 0 ? (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%', color: 'var(--text-muted)', fontSize: '14px' }}>
              Chưa có tin nhắn — hãy bắt đầu trò chuyện 👋
            </div>
          ) : (
            (() => {
              let latestMyId: number | null = null;
              if (activeChatType === 'private') {
                for (let i = messages.length - 1; i >= 0; i--) {
                  if (messages[i].senderName === username) {
                    latestMyId = messages[i].id;
                    break;
                  }
                }
              }
              return messages.map((msg) => {
                const isLatestMy = msg.id === latestMyId;
                const receiptStatus = isLatestMy ? (msg.id <= partnerLastReadId ? 'seen' : 'sent') : undefined;
                return (
                  <MessageBubble
                    key={msg.id}
                    msg={msg}
                    currentUsername={username}
                    translation={translations[msg.id]}
                    isTranslating={!!translatingIds[msg.id]}
                    receiptStatus={receiptStatus}
                    onTranslate={handleTranslateMessage}
                    onHideTranslation={handleHideTranslation}
                    onOpenSenderProfile={handleOpenSenderProfile}
                  />
                );
              });
            })()
          )}
          {activeChatType === 'room' && typingUsers.length > 0 && (
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
            placeholder={activeChatType === 'room' ? "Nhập tin nhắn… (gõ @copilot để hỏi AI)" : "Nhập tin nhắn..."}
          />
          <Button type="primary" icon={<SendOutlined />} onClick={() => handleSendMessage()} style={{ fontWeight: 500 }}>
            Gửi
          </Button>
        </div>
      </div>

      {activeChatType === 'room' && <CopilotPanel onTriggerCommand={handleSendMessage} />}
      {profileEverOpened && (
        <Suspense fallback={null}>
          <ProfileView
            userId={profileUserId || 0}
            isOpen={isProfileOpen}
            onClose={() => setIsProfileOpen(false)}
            currentUsername={username}
          />
        </Suspense>
      )}
    </div>
  );
};
