/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { useState } from 'react';
import { Form, Input, Button, Card, message } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import api from '../services/api';

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
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      height: '100vh',
      backgroundColor: '#0a0f1d',
      color: '#fff',
      fontFamily: 'monospace'
    }}>
      <Card
        title={
          <div style={{ color: '#a3e635', fontFamily: 'monospace', textAlign: 'center', fontSize: '18px', fontWeight: 'bold' }}>
            {isRegister ? '// REGISTER_SYS' : '// LOGIN_SYS'}
          </div>
        }
        bordered={true}
        style={{
          width: 380,
          backgroundColor: '#0f172a',
          borderColor: '#334155',
          borderRadius: 0,
          boxShadow: '0 0 15px rgba(163, 230, 53, 0.1)',
        }}
        styles={{
          header: {
            borderBottom: '1px solid #334155',
            backgroundColor: '#0f172a',
          },
          body: {
            backgroundColor: '#0f172a',
          }
        }}
      >
        <Form
          form={form}
          name="auth_form"
          onFinish={handleFinish}
          layout="vertical"
        >
          <Form.Item
            name="username"
            rules={[
              { required: true, message: 'Nhập tên đăng nhập!' },
              { min: 3, message: 'Tối thiểu 3 ký tự.' }
            ]}
          >
            <Input 
              prefix={<UserOutlined style={{ color: '#64748b' }} />} 
              placeholder="Username" 
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
            name="password"
            rules={[
              { required: true, message: 'Nhập mật khẩu!' },
              { min: 6, message: 'Mật khẩu cần tối thiểu 6 ký tự.' }
            ]}
          >
            <Input.Password
              prefix={<LockOutlined style={{ color: '#64748b' }} />}
              placeholder="Password"
              style={{
                backgroundColor: '#1e293b',
                borderColor: '#475569',
                color: '#fff',
                borderRadius: 0,
                fontFamily: 'monospace'
              }}
            />
          </Form.Item>

          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              loading={loading}
              style={{
                width: '100%',
                backgroundColor: '#a3e635',
                borderColor: '#a3e635',
                color: '#000',
                borderRadius: 0,
                fontWeight: 'bold',
                fontFamily: 'monospace',
                height: '40px'
              }}
            >
              {isRegister ? 'INITIALIZE_USER' : 'CONNECT_SESSION'}
            </Button>
          </Form.Item>
        </Form>
        <div style={{ textAlign: 'center', marginTop: '10px', fontSize: '12px', color: '#64748b' }}>
          {isRegister ? 'Đã có tài khoản? ' : 'Chưa có tài khoản? '}
          <span
            onClick={() => {
              setIsRegister(!isRegister);
              form.resetFields();
            }}
            style={{ color: '#a3e635', cursor: 'pointer', textDecoration: 'underline' }}
          >
            {isRegister ? 'Login' : 'Register'}
          </span>
        </div>
      </Card>
    </div>
  );
};
