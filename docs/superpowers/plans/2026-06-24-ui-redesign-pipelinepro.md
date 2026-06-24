# UI Redesign (PipelinePro-style, Teal, Light/Dark) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thay giao diện terminal của NexivraChat bằng giao diện thân thiện kiểu PipelinePro (teal, bo góc mềm, font Inter/Outfit, chữ tiếng Việt), hỗ trợ chuyển light/dark.

**Architecture:** Một lớp design token bằng CSS variables (2 theme qua `data-theme`), một `ThemeContext` quản lý theme + lưu localStorage, antd `ConfigProvider` đồng bộ component antd. Các view chỉ tiêu thụ token (`var(--...)`), không hardcode màu. Không đụng backend, SignalR, API.

**Tech Stack:** React 19 + TypeScript, antd, Vite, @ant-design/icons, Google Fonts (Inter, Outfit).

## Global Constraints

- **Purple Ban:** KHÔNG dùng tím/indigo (kể cả `#4F46E5`). Primary = teal `#0D9488`.
- Mọi màu trong inline style component dùng `var(--token)`, không hardcode hex (trừ file định nghĩa token và `App.tsx` colorPrimary).
- Font: Outfit (tiêu đề), Inter (nội dung). Bỏ toàn bộ `monospace`.
- Mặc định theme `light`; lưu `localStorage` key `nexivra-theme`; áp `data-theme` trước render (chống FOUC).
- KHÔNG đổi backend, schema DB, hợp đồng SignalR/API. KHÔNG sửa logic hook/effect trong `ChatView` (chỉ sửa phần render/JSX/màu/chữ).
- Không còn chữ kiểu terminal; viết lại sang tiếng Việt theo bảng trong từng task.
- Mỗi task kết thúc: `npx tsc --noEmit` và `npm run build` đều sạch (cảnh báo `@microsoft/signalr` INVALID_ANNOTATION và chunk-size có sẵn là chấp nhận được).

Thư mục frontend: `frontend/nexivra-chat-frontend/`. Mọi đường dẫn dưới đây tương đối tới thư mục này trừ khi ghi rõ.

---

### Task 1: Design tokens, fonts, base CSS

**Files:**
- Modify: `index.html`
- Modify: `src/index.css`

**Interfaces:**
- Produces: CSS variables dùng ở mọi task sau — `--bg-canvas`, `--bg-surface`, `--bg-elevated`, `--border`, `--text-primary`, `--text-secondary`, `--text-muted`, `--primary`, `--primary-hover`, `--primary-soft`, `--primary-on`, `--secondary`, `--accent`, `--bubble-me`, `--bubble-me-text`, `--bubble-other`, `--bubble-other-text`, `--bubble-ai-bg`, `--bubble-ai-border`, `--bubble-ai-text`. Class `.copilot-loading`.

- [ ] **Step 1: Nạp font + đổi title trong `index.html`**

Thay toàn bộ phần `<head>` của `index.html` thành:
```html
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=Outfit:wght@500;600;700&display=swap" rel="stylesheet" />
    <title>NexivraChat</title>
  </head>
```

- [ ] **Step 2: Thay toàn bộ `src/index.css`**

Ghi đè `src/index.css` bằng:
```css
:root[data-theme="light"] {
  --bg-canvas: #F8FAFC;
  --bg-surface: #FFFFFF;
  --bg-elevated: #F1F5F9;
  --border: #E2E8F0;
  --text-primary: #0F172A;
  --text-secondary: #475569;
  --text-muted: #94A3B8;
  --primary: #0D9488;
  --primary-hover: #0F766E;
  --primary-soft: #F0FDFA;
  --primary-on: #FFFFFF;
  --secondary: #0891B2;
  --accent: #F97316;
  --bubble-me: #0D9488;
  --bubble-me-text: #FFFFFF;
  --bubble-other: #FFFFFF;
  --bubble-other-text: #0F172A;
  --bubble-ai-bg: #F0FDFA;
  --bubble-ai-border: #99F6E4;
  --bubble-ai-text: #134E4A;
}

:root[data-theme="dark"] {
  --bg-canvas: #0B1220;
  --bg-surface: #111827;
  --bg-elevated: #1E293B;
  --border: #1E293B;
  --text-primary: #F1F5F9;
  --text-secondary: #94A3B8;
  --text-muted: #64748B;
  --primary: #14B8A6;
  --primary-hover: #0D9488;
  --primary-soft: #134E4A;
  --primary-on: #FFFFFF;
  --secondary: #22D3EE;
  --accent: #FB923C;
  --bubble-me: #0D9488;
  --bubble-me-text: #FFFFFF;
  --bubble-other: #1E293B;
  --bubble-other-text: #F1F5F9;
  --bubble-ai-bg: #134E4A;
  --bubble-ai-border: #0D9488;
  --bubble-ai-text: #CCFBF1;
}

html,
body,
#root {
  margin: 0;
  padding: 0;
  width: 100vw;
  height: 100vh;
  background-color: var(--bg-canvas);
  color: var(--text-primary);
  font-family: 'Inter', system-ui, -apple-system, sans-serif;
  overflow: hidden;
}

::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}

::-webkit-scrollbar-track {
  background: transparent;
}

::-webkit-scrollbar-thumb {
  background: var(--border);
  border-radius: 8px;
}

::-webkit-scrollbar-thumb:hover {
  background: var(--text-muted);
}

@keyframes pulse-soft {
  0%, 100% { opacity: 0.5; }
  50% { opacity: 1; }
}

.copilot-loading {
  animation: pulse-soft 1.2s infinite;
  font-size: 13px;
  color: var(--primary);
}
```

- [ ] **Step 3: Build kiểm tra**

Run (trong `frontend/nexivra-chat-frontend/`): `npx tsc --noEmit && npm run build`
Expected: không lỗi.

- [ ] **Step 4: Commit**

```bash
git add frontend/nexivra-chat-frontend/index.html frontend/nexivra-chat-frontend/src/index.css
git commit -m "feat(ui): add design tokens, fonts and light/dark base styles"
```

---

### Task 2: ThemeContext + getInitialTheme

**Files:**
- Create: `src/theme/ThemeContext.tsx`

**Interfaces:**
- Produces (dùng ở Task 3):
  - `function getInitialTheme(): 'light' | 'dark'`
  - `function ThemeProvider({ children }: { children: React.ReactNode }): JSX.Element`
  - `function useTheme(): { theme: 'light' | 'dark'; toggleTheme: () => void }`

- [ ] **Step 1: Tạo `src/theme/ThemeContext.tsx`**

```tsx
import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';

type Theme = 'light' | 'dark';
const STORAGE_KEY = 'nexivra-theme';

// Hàm thuần: đọc theme đã lưu, fallback 'light' nếu thiếu/không hợp lệ/localStorage lỗi.
export function getInitialTheme(): Theme {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === 'light' || saved === 'dark') return saved;
  } catch {
    // localStorage không khả dụng -> dùng mặc định
  }
  return 'light';
}

interface ThemeContextValue {
  theme: Theme;
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // bỏ qua nếu không ghi được
    }
  }, [theme]);

  const toggleTheme = () => setTheme((t) => (t === 'light' ? 'dark' : 'light'));

  return (
    <ThemeContext.Provider value={{ theme, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error('useTheme phải dùng bên trong ThemeProvider');
  return ctx;
}
```

- [ ] **Step 2: Build kiểm tra**

Run: `npx tsc --noEmit && npm run build`
Expected: không lỗi.

- [ ] **Step 3: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/theme/ThemeContext.tsx
git commit -m "feat(ui): add ThemeContext with persisted light/dark state"
```

---

### Task 3: ThemeToggle + wire App.tsx + main.tsx (FOUC guard)

**Files:**
- Create: `src/components/ThemeToggle.tsx`
- Modify: `src/main.tsx`
- Modify: `src/App.tsx`

**Interfaces:**
- Consumes: `getInitialTheme`, `ThemeProvider`, `useTheme` (Task 2).
- Produces (dùng ở Task 5): component `ThemeToggle` (đặt ở header phòng chat).

- [ ] **Step 1: Tạo `src/components/ThemeToggle.tsx`**

```tsx
import React from 'react';
import { Button } from 'antd';
import { BulbOutlined, BulbFilled } from '@ant-design/icons';
import { useTheme } from '../theme/ThemeContext';

export const ThemeToggle: React.FC = () => {
  const { theme, toggleTheme } = useTheme();
  return (
    <Button
      type="text"
      aria-label="Chuyển giao diện sáng/tối"
      icon={theme === 'dark' ? <BulbFilled /> : <BulbOutlined />}
      onClick={toggleTheme}
      style={{ color: 'var(--text-secondary)' }}
    />
  );
};
```

- [ ] **Step 2: Đặt `data-theme` trước render trong `src/main.tsx`**

Ghi đè `src/main.tsx`:
```tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { getInitialTheme } from './theme/ThemeContext'

document.documentElement.setAttribute('data-theme', getInitialTheme())

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
```

- [ ] **Step 3: Bọc ThemeProvider + ConfigProvider trong `src/App.tsx`**

Ghi đè `src/App.tsx`:
```tsx
import { useState } from 'react';
import { ConfigProvider, theme as antdTheme } from 'antd';
import { LoginView } from './views/LoginView';
import { ChatView } from './views/ChatView';
import { ThemeProvider, useTheme } from './theme/ThemeContext';

function AppShell() {
  const { theme } = useTheme();
  const [token, setToken] = useState<string | null>(localStorage.getItem('token'));
  const [username, setUsername] = useState<string | null>(localStorage.getItem('username'));

  const handleLoginSuccess = (usr: string, tkn: string) => {
    setUsername(usr);
    setToken(tkn);
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('username');
    setToken(null);
    setUsername(null);
  };

  return (
    <ConfigProvider
      theme={{
        algorithm: theme === 'dark' ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
        token: {
          colorPrimary: '#0D9488',
          borderRadius: 8,
          fontFamily: "'Inter', system-ui, sans-serif",
        },
      }}
    >
      {token && username ? (
        <ChatView username={username} token={token} onLogout={handleLogout} />
      ) : (
        <LoginView onLoginSuccess={handleLoginSuccess} />
      )}
    </ConfigProvider>
  );
}

function App() {
  return (
    <ThemeProvider>
      <AppShell />
    </ThemeProvider>
  );
}

export default App;
```

- [ ] **Step 4: Build kiểm tra**

Run: `npx tsc --noEmit && npm run build`
Expected: không lỗi.

- [ ] **Step 5: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/components/ThemeToggle.tsx frontend/nexivra-chat-frontend/src/main.tsx frontend/nexivra-chat-frontend/src/App.tsx
git commit -m "feat(ui): wire ThemeProvider, antd ConfigProvider and theme toggle"
```

---

### Task 4: Restyle LoginView (tokens + tiếng Việt)

**Files:**
- Modify: `src/views/LoginView.tsx`

**Interfaces:**
- Consumes: `useTheme` không cần; dùng `ThemeToggle` (Task 3) để có nút chuyển ở màn đăng nhập.
- Giữ nguyên: prop `onLoginSuccess`, logic `handleFinish`, `api.post` endpoints, lưu localStorage.

Bảng chữ: `// LOGIN_SYS`→`Đăng nhập`, `// REGISTER_SYS`→`Đăng ký`, `CONNECT_SESSION`→`Đăng nhập`, `INITIALIZE_USER`→`Tạo tài khoản`, placeholder `Username`→`Tên đăng nhập`, `Password`→`Mật khẩu`, link `Login`/`Register`→`Đăng nhập`/`Đăng ký`.

- [ ] **Step 1: Ghi đè `src/views/LoginView.tsx`**

```tsx
/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { useState } from 'react';
import { Form, Input, Button, Card, message } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import api from '../services/api';
import { ThemeToggle } from '../components/ThemeToggle';

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
      justifyContent: 'center',
      alignItems: 'center',
      height: '100vh',
      backgroundColor: 'var(--bg-canvas)',
      color: 'var(--text-primary)',
    }}>
      <div style={{ position: 'absolute', top: 16, right: 16 }}>
        <ThemeToggle />
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
          width: 380,
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
  );
};
```

- [ ] **Step 2: Build kiểm tra**

Run: `npx tsc --noEmit && npm run build`
Expected: không lỗi.

- [ ] **Step 3: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/views/LoginView.tsx
git commit -m "feat(ui): restyle LoginView with tokens and Vietnamese copy"
```

---

### Task 5: Restyle ChatView render (tokens + bubbles + tiếng Việt + ThemeToggle)

**Files:**
- Modify: `src/views/ChatView.tsx` (CHỈ phần `return (...)`; KHÔNG đụng state/hooks/effects phía trên)

**Interfaces:**
- Consumes: `ThemeToggle` (Task 3); các biến/handler đã có sẵn trong component: `rooms`, `activeRoomId`, `messages`, `inputText`, `onlineUsers`, `typingUsers`, `username`, `notifications`, `messageEndRef`, `handleInputChange`, `handleSendMessage`, `handleRoomCreated`, `setActiveRoomId`, `onLogout`, `activeRoom`.
- Giữ nguyên: toàn bộ hook/effect/handler; `RoomSidebar` và `CopilotPanel` vẫn được render như cũ với cùng props.

Bảng chữ: `#${room.name}`/`NO_ROOM_SELECTED`→`# Tên phòng`/`Chưa chọn phòng`; mô tả mặc định `Please select a room...`→`Chọn một phòng để bắt đầu trò chuyện`; empty `// NO_MESSAGES_YET - START_THE_CONVERSATION`→`Chưa có tin nhắn — hãy bắt đầu trò chuyện 👋`; `X đang gõ...` giữ; placeholder→`Nhập tin nhắn… (gõ @copilot để hỏi AI)`; `SEND`→`Gửi`.

- [ ] **Step 1: Thêm import ThemeToggle**

Thêm vào khối import đầu file (sau dòng import `CopilotPanel`):
```tsx
import { ThemeToggle } from '../components/ThemeToggle';
```

- [ ] **Step 2: Thay toàn bộ `return ( ... );` của component bằng JSX dưới đây**

Giữ nguyên mọi thứ phía trên `return (`. Thay từ `return (` đến `);` cuối component bằng:
```tsx
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
```

- [ ] **Step 3: Thêm import icon RobotOutlined**

Phần render dùng `<RobotOutlined />`. Sửa dòng import icon (hiện `import { SendOutlined } from '@ant-design/icons';`) thành:
```tsx
import { SendOutlined, RobotOutlined } from '@ant-design/icons';
```

- [ ] **Step 4: Build kiểm tra**

Run: `npx tsc --noEmit && npm run build`
Expected: không lỗi. (Nếu báo biến không dùng do bỏ style cũ, xoá biến thừa tương ứng.)

- [ ] **Step 5: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/views/ChatView.tsx
git commit -m "feat(ui): restyle ChatView with tokens, rounded bubbles and theme toggle"
```

---

### Task 6: Restyle RoomSidebar (tokens + tiếng Việt)

**Files:**
- Modify: `src/components/RoomSidebar.tsx`

**Interfaces:**
- Giữ nguyên: interface `Room`, props, `handleCreateRoom`, `api.post('/rooms')`.

Bảng chữ: `// CHANNELS_SYS`→`Phòng chat`, `#${room.name}`→`# ${room.name}`, `No description`→`Không có mô tả`, `CONNECTED_AS`→`Đang đăng nhập`, `// CREATE_NEW_CHANNEL`→`Tạo phòng mới`, `CHANNEL_NAME`→`Tên phòng`, `DESCRIPTION`→`Mô tả`, `CANCEL`→`Huỷ`, `INITIALIZE`→`Tạo phòng`, placeholder `e.g. dotnet-discussion`→`VD: thảo luận .NET`, `Room description...`→`Mô tả ngắn về phòng…`.

- [ ] **Step 1: Ghi đè `src/components/RoomSidebar.tsx`**

```tsx
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
```

- [ ] **Step 2: Build kiểm tra**

Run: `npx tsc --noEmit && npm run build`
Expected: không lỗi.

- [ ] **Step 3: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/components/RoomSidebar.tsx
git commit -m "feat(ui): restyle RoomSidebar with tokens and Vietnamese copy"
```

---

### Task 7: Restyle CopilotPanel (tokens + tiếng Việt)

**Files:**
- Modify: `src/components/CopilotPanel.tsx`

**Interfaces:**
- Giữ nguyên: prop `onTriggerCommand`, các chuỗi lệnh `@copilot ...` gửi đi (không đổi nội dung prompt), logic `handleExplainTerm`.

Bảng chữ: `// AI_COPILOT_HUD`→`Trợ lý AI`, `TÓM TẮT PHÒNG CHAT`→`Tóm tắt phòng`, `RUN_SUMMARIZE`→`Tóm tắt`, `GỢI Ý CHỦ ĐỀ CHAT`→`Gợi ý chủ đề`, `BRAINSTORM_TOPICS`→`Gợi ý`, `GIẢI THÍCH THUẬT NGỮ`→`Giải thích thuật ngữ`, `RUN`→`Hỏi`, `SYSTEM_ACTIVE v1.0.0`→`NexivraChat`, placeholder `e.g. SignalR` giữ.

- [ ] **Step 1: Ghi đè `src/components/CopilotPanel.tsx`**

```tsx
import React, { useState } from 'react';
import { Button, Input, Card, Divider } from 'antd';
import { RobotOutlined, BookOutlined, BulbOutlined, AlignLeftOutlined } from '@ant-design/icons';

interface CopilotPanelProps {
  onTriggerCommand: (command: string) => void;
}

export const CopilotPanel: React.FC<CopilotPanelProps> = ({ onTriggerCommand }) => {
  const [term, setTerm] = useState('');

  const handleExplainTerm = () => {
    if (term.trim()) {
      onTriggerCommand(`@copilot Giải thích thuật ngữ "${term.trim()}" ngắn gọn trong 2 câu.`);
      setTerm('');
    }
  };

  return (
    <div style={{ width: '280px', backgroundColor: 'var(--bg-surface)', borderLeft: '1px solid var(--border)', padding: '16px', display: 'flex', flexDirection: 'column', height: '100%', color: 'var(--text-primary)' }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
        <RobotOutlined style={{ color: 'var(--primary)', fontSize: '20px' }} />
        <span style={{ color: 'var(--text-primary)', fontWeight: 600, fontSize: '16px', fontFamily: "'Outfit', sans-serif" }}>
          Trợ lý AI
        </span>
      </div>

      <p style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: '1.5', margin: 0 }}>
        Trợ lý AI đồng hành trong phòng chat. Bạn có thể tag @copilot trực tiếp hoặc dùng các nút nhanh dưới đây:
      </p>

      <Divider style={{ borderColor: 'var(--border)', margin: '12px 0' }} />

      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', flex: 1 }}>
        <Card size="small" style={{ backgroundColor: 'var(--bg-elevated)', borderColor: 'var(--border)', borderRadius: 8 }} styles={{ body: { padding: '12px' } }}>
          <div style={{ color: 'var(--text-primary)', fontSize: '13px', fontWeight: 600, marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <AlignLeftOutlined /> Tóm tắt phòng
          </div>
          <p style={{ fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            Tóm tắt nhanh các tin nhắn gần đây trong phòng.
          </p>
          <Button block size="small" onClick={() => onTriggerCommand('@copilot Tóm tắt ngắn gọn các cuộc trò chuyện gần đây trong phòng này.')}>
            Tóm tắt
          </Button>
        </Card>

        <Card size="small" style={{ backgroundColor: 'var(--bg-elevated)', borderColor: 'var(--border)', borderRadius: 8 }} styles={{ body: { padding: '12px' } }}>
          <div style={{ color: 'var(--text-primary)', fontSize: '13px', fontWeight: 600, marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <BulbOutlined /> Gợi ý chủ đề
          </div>
          <p style={{ fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            AI gợi ý các chủ đề thảo luận tiếp theo cho phòng.
          </p>
          <Button block size="small" onClick={() => onTriggerCommand('@copilot Hãy gợi ý 3 ý tưởng/chủ đề thảo luận tiếp theo cho phòng chat này.')}>
            Gợi ý
          </Button>
        </Card>

        <Card size="small" style={{ backgroundColor: 'var(--bg-elevated)', borderColor: 'var(--border)', borderRadius: 8 }} styles={{ body: { padding: '12px' } }}>
          <div style={{ color: 'var(--text-primary)', fontSize: '13px', fontWeight: 600, marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <BookOutlined /> Giải thích thuật ngữ
          </div>
          <p style={{ fontSize: '11px', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            Nhập thuật ngữ cần AI giải nghĩa.
          </p>
          <div style={{ display: 'flex', gap: '4px' }}>
            <Input size="small" value={term} onChange={(e) => setTerm(e.target.value)} placeholder="VD: SignalR" onPressEnter={handleExplainTerm} />
            <Button type="primary" size="small" onClick={handleExplainTerm}>Hỏi</Button>
          </div>
        </Card>
      </div>

      <div style={{ fontSize: '11px', color: 'var(--text-muted)', textAlign: 'center', marginTop: 'auto' }}>
        NexivraChat
      </div>
    </div>
  );
};
```

- [ ] **Step 2: Build kiểm tra**

Run: `npx tsc --noEmit && npm run build`
Expected: không lỗi.

- [ ] **Step 3: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/components/CopilotPanel.tsx
git commit -m "feat(ui): restyle CopilotPanel with tokens and Vietnamese copy"
```

---

### Task 8: Cập nhật context.md + kiểm thử thủ công cả 2 theme

**Files:**
- Modify: `context.md` (gốc repo)

- [ ] **Step 1: Kiểm thử thủ công**

Chạy backend (`dotnet run --project backend/NexivraChatBackend`, cổng 5182) và frontend (`npm run dev`). Hard refresh. Kiểm tra ở **cả light và dark** (bấm nút bóng đèn ở header):
- Login, danh sách phòng, khung chat (bong bóng me/AI/other + typing + online count), panel Trợ lý AI.
- Reload trang → giữ đúng theme đã chọn.
- Không còn màu tím/indigo; không còn chữ terminal/monospace.
- Gõ `@copilot xin chào` vẫn stream phản hồi bình thường.

- [ ] **Step 2: Cập nhật `context.md`**

Trong `context.md`:
- Mục Tech Stack: ghi rõ UI mới dùng design system kiểu PipelinePro (teal `#0D9488`), font Inter/Outfit, hỗ trợ light/dark; cập nhật/loại bỏ ghi chú "phong cách Terminal" và "không dùng màu tím" giữ nguyên (vẫn đúng).
- Cây thư mục frontend: thêm `theme/ThemeContext.tsx` và `components/ThemeToggle.tsx`; cập nhật mô tả `LoginView.tsx`, `ChatView.tsx`, `RoomSidebar.tsx`, `CopilotPanel.tsx`, `index.css`, `App.tsx`, `main.tsx` cho khớp giao diện mới.

- [ ] **Step 3: Commit**

```bash
git add context.md
git commit -m "docs: update context.md for PipelinePro-style UI redesign"
```

---

## Notes
- Không thêm dependency mới (Inter/Outfit nạp qua CDN Google Fonts trong `index.html`).
- antd tự lo màu component qua `ConfigProvider` (colorPrimary teal + algorithm light/dark); inline style custom dùng token CSS.
- Toàn bộ thay đổi thuần giao diện + nội dung; không đụng backend/SignalR/logic.
