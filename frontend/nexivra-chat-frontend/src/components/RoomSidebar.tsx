/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { useState } from 'react';
import { Button, Modal, Form, Input, message, Badge } from 'antd';
import { PlusOutlined, LogoutOutlined, CommentOutlined } from '@ant-design/icons';
import api from '../services/api';
import { Logo } from './Logo';

export interface Room {
  id: number;
  name: string;
  description: string;
}

export interface SidebarUser {
  id: number;
  username: string;
  isOnline?: boolean;
}

interface RoomSidebarProps {
  rooms: Room[];
  users: SidebarUser[];
  activeRoomId: number | null;
  activeChatType: 'room' | 'private';
  onSelectRoom: (roomId: number) => void;
  onSelectUser: (userId: number) => void;
  onRoomCreated: (newRoom: Room) => void;
  onLogout: () => void;
  username: string;
  onOpenProfile: (userId: number) => void;
  // Unread: phòng key theo roomId, DM key theo id người đối thoại.
  unreadRooms?: Record<number, number>;
  unreadPrivateChats?: Record<number, number>;
  // id người đối thoại của DM đang mở (để ẩn badge hội thoại active).
  activePrivateUserId?: number | null;
  mentionRooms?: Set<number>;
}

export const RoomSidebar: React.FC<RoomSidebarProps> = ({
  rooms,
  users,
  activeRoomId,
  activeChatType,
  onSelectRoom,
  onSelectUser,
  onRoomCreated,
  onLogout,
  username,
  onOpenProfile,
  unreadRooms = {},
  unreadPrivateChats = {},
  activePrivateUserId = null,
  mentionRooms = new Set(),
}) => {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();

  const handleCreateRoom = async (values: any) => {
    setLoading(true);
    try {
      const response = await api.post('/rooms', { name: values.name, description: values.description });
      message.success('Tạo phòng thành công!');
      onRoomCreated(response.data);
      setIsModalOpen(false);
      form.resetFields();
    } catch (error: any) {
      const errorMsg = error.response?.data || 'Không thể tạo phòng chat.';
      message.error(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const initials = username.slice(0, 2).toUpperCase();

  return (
    <div style={{ width: '260px', backgroundColor: 'var(--bg-elevated)', borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Brand Logo */}
      <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center' }}>
        <Logo height={26} />
      </div>

      {/* Header */}
      <div style={{ padding: '10px 16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span style={{ color: 'var(--text-primary)', fontWeight: 600, fontSize: '14px', fontFamily: "'Outfit', sans-serif" }}>
          Hội thoại
        </span>
        <Button type="text" icon={<PlusOutlined style={{ color: 'var(--primary)' }} />} onClick={() => setIsModalOpen(true)} title="Tạo phòng mới" />
      </div>

      {/* Lists (Rooms & Users) */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '12px', display: 'flex', flexDirection: 'column', gap: '20px' }}>
        {/* Rooms Section */}
        <div>
          <div style={{ padding: '0 12px 8px 12px', fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
            Kênh chat
          </div>
          {rooms.map((room) => {
            const isActive = activeChatType === 'room' && room.id === activeRoomId;
            const roomUnread = isActive ? 0 : (unreadRooms[room.id] || 0);
            const hasMention = !isActive && mentionRooms.has(room.id);
            return (
              <div
                key={room.id}
                onClick={() => onSelectRoom(room.id)}
                role="button"
                aria-label={roomUnread > 0 ? `# ${room.name}, ${roomUnread} tin chưa đọc` : `# ${room.name}`}
                style={{
                  padding: '9px 12px',
                  cursor: 'pointer',
                  backgroundColor: isActive ? 'var(--primary-soft)' : 'transparent',
                  borderLeft: isActive ? '3px solid var(--primary)' : '3px solid transparent',
                  color: isActive ? 'var(--primary)' : 'var(--text-secondary)',
                  marginBottom: '4px',
                  borderRadius: 8,
                  transition: 'all 0.15s ease',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px'
                }}
              >
                <CommentOutlined />
                <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', flex: 1 }}>
                  {/* Phòng có tin chưa đọc: chữ đậm hơn (tín hiệu nhẹ, không to như DM) */}
                  <div style={{ fontWeight: isActive || roomUnread > 0 ? 600 : 400, fontSize: '13px', color: roomUnread > 0 && !isActive ? 'var(--text-primary)' : undefined }}># {room.name}</div>
                </div>
                {hasMention && (
                  <span
                    title="Có ai đó vừa nhắc bạn trong phòng này"
                    style={{
                      padding: '0 5px',
                      borderRadius: '10px',
                      backgroundColor: '#0D9488',
                      color: '#ffffff',
                      fontSize: '11px',
                      fontWeight: 700,
                      flexShrink: 0,
                      lineHeight: '16px'
                    }}
                  >
                    @
                  </span>
                )}
                {roomUnread > 0 && !hasMention && (
                  <span
                    aria-hidden="true"
                    style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: 'var(--primary)', flexShrink: 0 }}
                  />
                )}
              </div>
            );
          })}
        </div>

        {/* Users Section */}
        <div>
          <div style={{ padding: '0 12px 8px 12px', fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
            Tin nhắn riêng
          </div>
          {users.map((u) => {
            const isActive = activeChatType === 'private' && u.id === activePrivateUserId;
            const dmUnread = isActive ? 0 : (unreadPrivateChats[u.id] || 0);
            return (
              <div
                key={u.id}
                onClick={() => onSelectUser(u.id)}
                role="button"
                aria-label={dmUnread > 0 ? `${u.username}, ${dmUnread} tin chưa đọc` : u.username}
                style={{
                  padding: '9px 12px',
                  cursor: 'pointer',
                  backgroundColor: isActive ? 'var(--primary-soft)' : 'transparent',
                  borderLeft: isActive ? '3px solid var(--primary)' : '3px solid transparent',
                  color: isActive ? 'var(--primary)' : 'var(--text-secondary)',
                  marginBottom: '4px',
                  borderRadius: 8,
                  transition: 'all 0.15s ease',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: '8px'
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', overflow: 'hidden' }}>
                  {/* Chấm online cạnh tên (nhường slot phải cho badge unread) */}
                  <span
                    style={{
                      width: '7px',
                      height: '7px',
                      borderRadius: '50%',
                      backgroundColor: u.isOnline ? 'var(--primary)' : 'var(--text-muted)',
                      display: 'inline-block',
                      flexShrink: 0
                    }}
                  />
                  <span style={{ fontWeight: isActive || dmUnread > 0 ? 600 : 400, fontSize: '13px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', color: dmUnread > 0 && !isActive ? 'var(--text-primary)' : undefined }}>
                    {u.username}
                  </span>
                </div>
                {/* DM "to tiếng" hơn phòng: badge SỐ (teal đậm để chữ trắng đủ tương phản) */}
                {dmUnread > 0 && (
                  <Badge
                    count={dmUnread}
                    overflowCount={99}
                    style={{ backgroundColor: '#0F766E', flexShrink: 0 }}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Footer Profile */}
      <div style={{ padding: '12px 16px', borderTop: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div 
          onClick={() => onOpenProfile(0)}
          style={{ display: 'flex', alignItems: 'center', gap: '10px', overflow: 'hidden', cursor: 'pointer' }}
          title="Xem hồ sơ của bạn"
        >
          <span style={{ width: 32, height: 32, borderRadius: '50%', background: 'var(--primary-soft)', color: 'var(--primary)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 600, flexShrink: 0 }}>
            {initials}
          </span>
          <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Đang đăng nhập</div>
            <div style={{ color: 'var(--text-primary)', fontWeight: 600, fontSize: '13px' }}>{username}</div>
          </div>
        </div>
        <Button type="text" danger icon={<LogoutOutlined />} onClick={onLogout} />
      </div>

      {/* Create Room Modal */}
      <Modal
        title="Tạo phòng mới"
        open={isModalOpen}
        onCancel={() => setIsModalOpen(false)}
        footer={null}
      >
        <Form form={form} layout="vertical" onFinish={handleCreateRoom} style={{ marginTop: '16px' }}>
          <Form.Item label="Tên phòng" name="name" rules={[{ required: true, message: 'Nhập tên phòng!' }]}>
            <Input placeholder="VD: thảo luận .NET" />
          </Form.Item>

          <Form.Item label="Mô tả" name="description">
            <Input placeholder="Mô tả ngắn về phòng…" />
          </Form.Item>

          <Form.Item style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 0 }}>
            <Button onClick={() => setIsModalOpen(false)} style={{ marginRight: '8px' }}>Huỷ</Button>
            <Button type="primary" htmlType="submit" loading={loading}>Tạo phòng</Button>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};
