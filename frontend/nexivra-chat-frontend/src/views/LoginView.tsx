/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { useState } from 'react';
import { Form, Input, Button, Card, message } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import api from '../services/api';
import { ThemeToggle } from '../components/ThemeToggle';
import { Logo } from '../components/Logo';

interface LoginViewProps {
  onLoginSuccess: (username: string, token: string) => void;
}

export const LoginView: React.FC<LoginViewProps> = ({ onLoginSuccess }) => {
  const [isRegister, setIsRegister] = useState(false);
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();

  const handleFinish = async (values: any) => {
    setLoading(true);
    const endpoint = isRegister ? '/auth/register' : '/auth/login';
    try {
      const response = await api.post(endpoint, {
        username: values.username,
        password: values.password,
      });

      const { username, token } = response.data;
      localStorage.setItem('token', token);
      localStorage.setItem('username', username);
      message.success(isRegister ? 'Đăng ký tài khoản thành công!' : 'Đăng nhập thành công!');
      onLoginSuccess(username, token);
    } catch (error: any) {
      const errorMsg = error.response?.data || 'Đã xảy ra lỗi, vui lòng thử lại.';
      message.error(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      position: 'relative',
      display: 'flex',
      flexDirection: 'column',
      justifyContent: 'center',
      alignItems: 'center',
      minHeight: '100dvh',
      backgroundColor: 'var(--bg-canvas)',
      color: 'var(--text-primary)',
      padding: '24px 16px',
      boxSizing: 'border-box',
    }}>
      <div style={{ position: 'absolute', top: 16, right: 16 }}>
        <ThemeToggle />
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', width: '100%', maxWidth: 380 }}>
        <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 24 }}>
          <Logo height={44} />
        </div>

        <Card
          title={
            <div style={{
              textAlign: 'center',
              fontSize: '20px',
              fontWeight: 600,
              fontFamily: "'Outfit', sans-serif",
              color: 'var(--text-primary)',
            }}>
              {isRegister ? 'Đăng ký' : 'Đăng nhập'}
            </div>
          }
          style={{
            width: '100%',
            backgroundColor: 'var(--bg-surface)',
            borderColor: 'var(--border)',
            borderRadius: 12,
          }}
        >
        <Form form={form} name="auth_form" onFinish={handleFinish} layout="vertical">
          <Form.Item
            name="username"
            rules={[
              { required: true, message: 'Nhập tên đăng nhập!' },
              { min: 3, message: 'Tối thiểu 3 ký tự.' }
            ]}
          >
            <Input prefix={<UserOutlined style={{ color: 'var(--text-muted)' }} />} placeholder="Tên đăng nhập" />
          </Form.Item>

          <Form.Item
            name="password"
            rules={[
              { required: true, message: 'Nhập mật khẩu!' },
              { min: 6, message: 'Mật khẩu cần tối thiểu 6 ký tự.' }
            ]}
          >
            <Input.Password prefix={<LockOutlined style={{ color: 'var(--text-muted)' }} />} placeholder="Mật khẩu" />
          </Form.Item>

          <Form.Item>
            <Button type="primary" htmlType="submit" loading={loading} block style={{ height: 42, fontWeight: 500 }}>
              {isRegister ? 'Tạo tài khoản' : 'Đăng nhập'}
            </Button>
          </Form.Item>
        </Form>

        <div style={{ textAlign: 'center', marginTop: '10px', fontSize: '13px', color: 'var(--text-secondary)' }}>
          {isRegister ? 'Đã có tài khoản? ' : 'Chưa có tài khoản? '}
          <span
            onClick={() => { setIsRegister(!isRegister); form.resetFields(); }}
            style={{ color: 'var(--primary)', cursor: 'pointer', fontWeight: 500 }}
          >
            {isRegister ? 'Đăng nhập' : 'Đăng ký'}
          </span>
        </div>
      </Card>
      </div>
    </div>
  );
};
