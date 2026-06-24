/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { useState } from 'react';
import { Button, Modal, Form, Input, message } from 'antd';
import { PlusOutlined, LogoutOutlined, CommentOutlined } from '@ant-design/icons';
import api from '../services/api';

export interface Room {
  id: number;
  name: string;
  description: string;
}

interface RoomSidebarProps {
  rooms: Room[];
  activeRoomId: number | null;
  onSelectRoom: (roomId: number) => void;
  onRoomCreated: (newRoom: Room) => void;
  onLogout: () => void;
  username: string;
}

export const RoomSidebar: React.FC<RoomSidebarProps> = ({
  rooms,
  activeRoomId,
  onSelectRoom,
  onRoomCreated,
  onLogout,
  username
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
      {/* Header */}
      <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span style={{ color: 'var(--text-primary)', fontWeight: 600, fontSize: '16px', fontFamily: "'Outfit', sans-serif" }}>
          Phòng chat
        </span>
        <Button type="text" icon={<PlusOutlined style={{ color: 'var(--primary)' }} />} onClick={() => setIsModalOpen(true)} />
      </div>

      {/* Room List */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '12px' }}>
        {rooms.map((room) => {
          const isActive = room.id === activeRoomId;
          return (
            <div
              key={room.id}
              onClick={() => onSelectRoom(room.id)}
              style={{
                padding: '10px 12px',
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
              <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                <div style={{ fontWeight: isActive ? 600 : 400, fontSize: '14px' }}># {room.name}</div>
                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{room.description || 'Không có mô tả'}</div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Footer Profile */}
      <div style={{ padding: '12px 16px', borderTop: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', overflow: 'hidden' }}>
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
