/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { useState } from 'react';
import { Button, Modal, Form, Input, message, ConfigProvider } from 'antd';
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
      const response = await api.post('/rooms', {
        name: values.name,
        description: values.description
      });
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

  return (
    <div style={{
      width: '260px',
      backgroundColor: '#0f172a',
      borderRight: '1px solid #1e293b',
      display: 'flex',
      flexDirection: 'column',
      height: '100%',
      fontFamily: 'monospace'
    }}>
      {/* Header */}
      <div style={{
        padding: '16px',
        borderBottom: '1px solid #1e293b',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center'
      }}>
        <span style={{ color: '#a3e635', fontWeight: 'bold', fontSize: '15px' }}>
          // CHANNELS_SYS
        </span>
        <Button
          type="text"
          icon={<PlusOutlined style={{ color: '#a3e635' }} />}
          onClick={() => setIsModalOpen(true)}
          style={{ borderRadius: 0 }}
        />
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
                backgroundColor: isActive ? '#1e293b' : 'transparent',
                borderLeft: isActive ? '3px solid #a3e635' : '3px solid transparent',
                color: isActive ? '#a3e635' : '#94a3b8',
                marginBottom: '4px',
                transition: 'all 0.2s ease',
                display: 'flex',
                alignItems: 'center',
                gap: '8px'
              }}
              onMouseEnter={(e) => {
                if (!isActive) {
                  e.currentTarget.style.color = '#fff';
                  e.currentTarget.style.backgroundColor = '#1e293b50';
                }
              }}
              onMouseLeave={(e) => {
                if (!isActive) {
                  e.currentTarget.style.color = '#94a3b8';
                  e.currentTarget.style.backgroundColor = 'transparent';
                }
              }}
            >
              <CommentOutlined />
              <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                <div style={{ fontWeight: isActive ? 'bold' : 'normal', fontSize: '13px' }}>#{room.name}</div>
                <div style={{ fontSize: '10px', color: '#64748b' }}>{room.description || 'No description'}</div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Footer Profile */}
      <div style={{
        padding: '16px',
        borderTop: '1px solid #1e293b',
        backgroundColor: '#0b0f19',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between'
      }}>
        <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', marginRight: '8px' }}>
          <div style={{ fontSize: '11px', color: '#64748b' }}>CONNECTED_AS</div>
          <div style={{ color: '#fff', fontWeight: 'bold', fontSize: '13px' }}>{username}</div>
        </div>
        <Button
          type="text"
          danger
          icon={<LogoutOutlined />}
          onClick={onLogout}
          style={{ borderRadius: 0 }}
        />
      </div>

      {/* Create Room Modal */}
      <ConfigProvider
        theme={{
          token: {
            colorBgElevated: '#0f172a',
            colorTextHeading: '#a3e635',
            colorText: '#fff',
            borderRadiusLG: 0,
          }
        }}
      >
        <Modal
          title={<span style={{ color: '#a3e635', fontFamily: 'monospace' }}>// CREATE_NEW_CHANNEL</span>}
          open={isModalOpen}
          onCancel={() => setIsModalOpen(false)}
          footer={null}
          style={{ border: '1px solid #334155', borderRadius: 0, padding: 0 }}
          styles={{
            mask: { backgroundColor: 'rgba(0, 0, 0, 0.8)' }
          }}
        >
          <Form
            form={form}
            layout="vertical"
            onFinish={handleCreateRoom}
            style={{ marginTop: '16px' }}
          >
            <Form.Item
              label={<span style={{ color: '#94a3b8', fontFamily: 'monospace' }}>CHANNEL_NAME</span>}
              name="name"
              rules={[{ required: true, message: 'Nhập tên phòng!' }]}
            >
              <Input
                placeholder="e.g. dotnet-discussion"
                style={{
                  backgroundColor: '#1e293b',
                  borderColor: '#475569',
                  color: '#fff',
                  borderRadius: 0,
                  fontFamily: 'monospace'
                }}
              />
            </Form.Item>

            <Form.Item
              label={<span style={{ color: '#94a3b8', fontFamily: 'monospace' }}>DESCRIPTION</span>}
              name="description"
            >
              <Input
                placeholder="Room description..."
                style={{
                  backgroundColor: '#1e293b',
                  borderColor: '#475569',
                  color: '#fff',
                  borderRadius: 0,
                  fontFamily: 'monospace'
                }}
              />
            </Form.Item>

            <Form.Item style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginBottom: 0 }}>
              <Button
                onClick={() => setIsModalOpen(false)}
                style={{
                  backgroundColor: 'transparent',
                  borderColor: '#475569',
                  color: '#94a3b8',
                  borderRadius: 0,
                  fontFamily: 'monospace',
                  marginRight: '8px'
                }}
              >
                CANCEL
              </Button>
              <Button
                type="primary"
                htmlType="submit"
                loading={loading}
                style={{
                  backgroundColor: '#a3e635',
                  borderColor: '#a3e635',
                  color: '#000',
                  borderRadius: 0,
                  fontWeight: 'bold',
                  fontFamily: 'monospace'
                }}
              >
                INITIALIZE
              </Button>
            </Form.Item>
          </Form>
        </Modal>
      </ConfigProvider>
    </div>
  );
};
