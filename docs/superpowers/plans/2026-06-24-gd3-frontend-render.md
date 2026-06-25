# Giai đoạn 3 — Frontend Render Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Giảm re-render và bundle phía frontend: tách `MessageBubble` (memo hóa) để mỗi token AI chỉ re-render 1 bong bóng, và lazy-load `ProfileView` để tách chunk antd nặng khỏi bundle ban đầu.

**Architecture:** Trích phần render mỗi tin nhắn trong `ChatView` ra component `MessageBubble` bọc `React.memo`; các handler truyền xuống được bọc `useCallback` (props ổn định) và dùng `usersRef` để callback mở-profile-theo-tên không bị tạo lại khi presence cập nhật → memo giữ hiệu lực. `ProfileView` chuyển sang `React.lazy` + `Suspense`, chỉ mount sau lần đầu người dùng mở hồ sơ (giữ animation đóng/mở của Modal). KHÔNG virtualize (chỉ tải 50 tin/phòng — YAGNI).

**Tech Stack:** React 19, TypeScript, Vite, antd.

## Global Constraints

- Không đổi hành vi/UX hiện có: chat nhóm & 1-1, stream AI, dịch tin, mở profile, presence, typing, auto-scroll (đã sửa ở GĐ1) phải giữ nguyên.
- Giữ nguyên y hệt style inline và class (`copilot-loading`, `fadeIn`) của bong bóng — chỉ di chuyển code, không đổi giao diện.
- Không thêm thư viện mới (KHÔNG dùng react-virtual). Không thêm tính năng.
- Không có test tự động cho frontend → verification mỗi task = `npm run build` (0 lỗi TypeScript) + smoke test thủ công. Nếu không chạy được app thì SKIP smoke và ghi chú, KHÔNG block.
- Lệnh build chạy từ `frontend/nexivra-chat-frontend`. KHÔNG tạo file log trong repo.
- `ChatView.tsx` đã có sẵn các comment `/* eslint-disable react-hooks/exhaustive-deps */` ở đầu file — giữ nguyên.

---

### Task 1: Tách `MessageBubble` + `React.memo` + `useCallback`

Trích JSX render mỗi tin nhắn (hiện inline trong `messages.map`, dòng 466-571 của `ChatView.tsx`) ra component memo hóa; ổn định hóa các handler để memo có hiệu lực trong lúc AI stream.

**Files:**
- Create: `frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx`
- Modify: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`

**Interfaces:**
- Produces: `MessageBubble` (memoized) với props:
  - `msg: Message`
  - `currentUsername: string`
  - `translation: string | undefined`
  - `isTranslating: boolean`
  - `onTranslate: (msgId: number, text: string) => void`
  - `onHideTranslation: (msgId: number) => void`
  - `onOpenSenderProfile: (senderName: string) => void`
- Consumes: `type { Message }` export sẵn từ `ChatView.tsx`.

- [ ] **Step 1: Tạo `MessageBubble.tsx`**

Tạo `frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx`:
```tsx
import React from 'react';
import { RobotOutlined, TranslationOutlined } from '@ant-design/icons';
import type { Message } from '../views/ChatView';

interface MessageBubbleProps {
  msg: Message;
  currentUsername: string;
  translation: string | undefined;
  isTranslating: boolean;
  onTranslate: (msgId: number, text: string) => void;
  onHideTranslation: (msgId: number) => void;
  onOpenSenderProfile: (senderName: string) => void;
}

const MessageBubbleComponent: React.FC<MessageBubbleProps> = ({
  msg,
  currentUsername,
  translation,
  isTranslating,
  onTranslate,
  onHideTranslation,
  onOpenSenderProfile,
}) => {
  const isMe = msg.senderName === currentUsername;
  const isAi = msg.isAi;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignSelf: isMe ? 'flex-end' : 'flex-start', maxWidth: '74%', alignItems: isMe ? 'flex-end' : 'flex-start' }}>
      <div style={{ fontSize: '11px', color: isAi ? 'var(--primary)' : 'var(--text-muted)', marginBottom: '4px', display: 'flex', gap: '6px', alignItems: 'center' }}>
        {isAi && <RobotOutlined />}
        <span
          style={{ cursor: isAi ? 'default' : 'pointer', textDecoration: isAi ? 'none' : 'underline' }}
          onClick={() => {
            if (!isAi) {
              onOpenSenderProfile(msg.senderName);
            }
          }}
          title={isAi ? "Trợ lý AI" : `Xem hồ sơ của @${msg.senderName}`}
        >
          {isAi ? 'Trợ lý AI' : msg.senderName}
        </span>
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
      {!isMe && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '4px', fontSize: '11px' }}>
          {isTranslating ? (
            <span style={{ color: 'var(--primary)', fontStyle: 'italic' }}>Đang dịch...</span>
          ) : translation ? (
            <button
              onClick={() => onHideTranslation(msg.id)}
              style={{ background: 'none', border: 'none', padding: 0, color: 'var(--text-muted)', cursor: 'pointer', textDecoration: 'underline' }}
            >
              Ẩn bản dịch
            </button>
          ) : (
            <button
              onClick={() => onTranslate(msg.id, msg.content)}
              style={{ background: 'none', border: 'none', padding: 0, color: 'var(--primary)', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '3px' }}
            >
              <TranslationOutlined /> Dịch
            </button>
          )}
        </div>
      )}
      {translation && (
        <div style={{ marginTop: '6px', padding: '8px 12px', backgroundColor: 'var(--bg-elevated)', borderLeft: '3px solid var(--primary)', borderRadius: '4px 12px 12px 12px', fontSize: '13px', color: 'var(--text-secondary)', lineHeight: '1.4', maxWidth: '100%', animation: 'fadeIn 0.2s ease-out' }}>
          <div style={{ fontSize: '10px', color: 'var(--text-muted)', marginBottom: '3px', fontWeight: 600 }}>
            BẢN DỊCH
          </div>
          {translation}
        </div>
      )}
    </div>
  );
};

export const MessageBubble = React.memo(MessageBubbleComponent);
```

- [ ] **Step 2: Cập nhật import trong `ChatView.tsx`**

Dòng 4 — thêm `useCallback`:
```tsx
import React, { useState, useEffect, useRef, useCallback } from 'react';
```
Dòng 6 — bỏ `RobotOutlined, TranslationOutlined` (đã chuyển sang MessageBubble), chỉ giữ `SendOutlined`:
```tsx
import { SendOutlined } from '@ant-design/icons';
```
Thêm import MessageBubble (đặt cạnh các import component, ví dụ ngay sau dòng `import { ThemeToggle } ...` dòng 11):
```tsx
import { MessageBubble } from '../components/MessageBubble';
```

- [ ] **Step 3: Thêm `usersRef` (giữ callback ổn định) sau khối refs**

Ngay sau dòng `const prevMessageCountRef = useRef(0);` (dòng 65), thêm:
```tsx
  // Giữ bản tham chiếu users mới nhất để callback mở-profile-theo-tên ổn định
  // (không tạo lại khi presence cập nhật), nhờ đó React.memo của MessageBubble giữ hiệu lực.
  const usersRef = useRef(users);
  useEffect(() => { usersRef.current = users; }, [users]);
```

- [ ] **Step 4: Bọc `handleOpenProfile` bằng `useCallback`; thêm `handleOpenSenderProfile`**

Thay khối `handleOpenProfile` hiện tại (dòng 49-52):
```tsx
  const handleOpenProfile = (userId: number) => {
    setProfileUserId(userId);
    setIsProfileOpen(true);
  };
```
bằng:
```tsx
  const handleOpenProfile = useCallback((userId: number) => {
    setProfileUserId(userId);
    setIsProfileOpen(true);
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
```

- [ ] **Step 5: Bọc `handleTranslateMessage` bằng `useCallback`; thêm `handleHideTranslation`**

Thay signature dòng 344:
```tsx
  const handleTranslateMessage = async (msgId: number, text: string) => {
```
bằng:
```tsx
  const handleTranslateMessage = useCallback(async (msgId: number, text: string) => {
```
và đổi dòng đóng hàm (dòng 357) từ:
```tsx
  };
```
thành:
```tsx
  }, []);
```
Ngay sau đó, thêm handler ẩn bản dịch:
```tsx
  const handleHideTranslation = useCallback((msgId: number) => {
    setTranslations((prev) => {
      const copy = { ...prev };
      delete copy[msgId];
      return copy;
    });
  }, []);
```

- [ ] **Step 6: Thay JSX bong bóng inline bằng `<MessageBubble>`**

Trong khối "Message Area", thay toàn bộ phần `messages.map((msg) => { ... })` (dòng 466-571, tức từ `messages.map((msg) => {` tới `})` đóng map) bằng:
```tsx
            messages.map((msg) => (
              <MessageBubble
                key={msg.id}
                msg={msg}
                currentUsername={username}
                translation={translations[msg.id]}
                isTranslating={!!translatingIds[msg.id]}
                onTranslate={handleTranslateMessage}
                onHideTranslation={handleHideTranslation}
                onOpenSenderProfile={handleOpenSenderProfile}
              />
            ))
```
(Giữ nguyên phần `messages.length === 0 ? (...) : (` bao quanh và phần typing indicator + `<div ref={messageEndRef} />` phía dưới.)

- [ ] **Step 7: Build**

Run: `cd frontend/nexivra-chat-frontend && npm run build`
Expected: build thành công, 0 lỗi TypeScript. (Không còn cảnh báo "RobotOutlined/TranslationOutlined is declared but never used" trong ChatView.)

- [ ] **Step 8: Smoke test thủ công (tùy chọn)**

Run: `npm run dev` (kèm backend). Mở React DevTools Profiler, gửi `@copilot <câu dài>`.
Expected: khi token AI đổ về, chỉ bong bóng AI đang stream re-render (các bong bóng khác không); dịch tin, ẩn bản dịch, click tên mở hồ sơ, chat 1-1 vẫn hoạt động như cũ. Nếu không chạy được → SKIP và ghi chú.

- [ ] **Step 9: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx frontend/nexivra-chat-frontend/src/views/ChatView.tsx
git commit -m "perf(ui): tách MessageBubble + React.memo, ổn định callback bằng useCallback"
```

---

### Task 2: Lazy-load `ProfileView`

`ProfileView` dùng nhiều antd nặng (Modal, Progress, Select, Tag, Spin). Tách thành chunk riêng, chỉ tải sau lần đầu người dùng mở hồ sơ; giữ animation Modal bằng cách mount vĩnh viễn sau lần mở đầu tiên.

**Files:**
- Modify: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`

**Interfaces:**
- Consumes: `ProfileView` (named export) từ `./ProfileView`; props không đổi (`userId`, `isOpen`, `onClose`, `currentUsername`). Dựa trên `handleOpenProfile` (đã là `useCallback` sau Task 1).

- [ ] **Step 1: Đổi import React để thêm `lazy, Suspense`**

Thay dòng import React (sau Task 1 đang là `import React, { useState, useEffect, useRef, useCallback } from 'react';`) thành:
```tsx
import React, { useState, useEffect, useRef, useCallback, lazy, Suspense } from 'react';
```

- [ ] **Step 2: Thay import tĩnh `ProfileView` bằng `lazy`**

Xóa dòng `import { ProfileView } from './ProfileView';` (dòng 12).
Thêm khai báo lazy ngay sau khối import (trước `export interface Message`), xử lý named export:
```tsx
const ProfileView = lazy(() =>
  import('./ProfileView').then((m) => ({ default: m.ProfileView }))
);
```

- [ ] **Step 3: Thêm state `profileEverOpened` và set khi mở hồ sơ**

Sau dòng `const [isProfileOpen, setIsProfileOpen] = useState(false);` (dòng 47), thêm:
```tsx
  // Chỉ mount ProfileView (lazy chunk) sau lần đầu mở hồ sơ; giữ mount để Modal có animation đóng/mở.
  const [profileEverOpened, setProfileEverOpened] = useState(false);
```
Trong `handleOpenProfile` (đã là useCallback từ Task 1), thêm `setProfileEverOpened(true);`:
```tsx
  const handleOpenProfile = useCallback((userId: number) => {
    setProfileUserId(userId);
    setIsProfileOpen(true);
    setProfileEverOpened(true);
  }, []);
```

- [ ] **Step 4: Render `ProfileView` qua `Suspense`, chỉ khi đã từng mở**

Thay khối JSX hiện tại (dòng 596-601):
```tsx
      <ProfileView
        userId={profileUserId || 0}
        isOpen={isProfileOpen}
        onClose={() => setIsProfileOpen(false)}
        currentUsername={username}
      />
```
bằng:
```tsx
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
```

- [ ] **Step 5: Build (xác nhận tách chunk)**

Run: `cd frontend/nexivra-chat-frontend && npm run build`
Expected: build thành công, 0 lỗi TypeScript; trong output Vite xuất hiện một chunk riêng cho `ProfileView` (ví dụ `ProfileView-XXXX.js`) tách khỏi chunk chính.

- [ ] **Step 6: Smoke test thủ công (tùy chọn)**

Run: `npm run dev`. Tải app, mở tab Network; xác nhận chunk `ProfileView` CHƯA tải khi vào chat; click vào tên người dùng/hồ sơ → chunk tải, Modal hồ sơ mở đúng, đóng/mở lại bình thường. Nếu không chạy được → SKIP và ghi chú.

- [ ] **Step 7: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/views/ChatView.tsx
git commit -m "perf(ui): lazy-load ProfileView bằng React.lazy + Suspense"
```

---

## Self-Review (đã thực hiện)

- **Spec coverage:** GĐ3 (phạm vi đã chốt với người dùng: BỎ virtualize) gồm: tách `MessageBubble` + `React.memo` + `useCallback` (Task 1); lazy-load `ProfileView` (Task 2). Đủ. Virtualize bị loại theo quyết định YAGNI của người dùng.
- **Placeholder scan:** Không TBD/TODO; mọi step có code cụ thể.
- **Type consistency:** Props `MessageBubble` (Task 1) khớp giữa định nghĩa component và chỗ dùng trong `ChatView`; `onOpenSenderProfile(senderName: string)` dùng `usersRef`/`username`; `handleTranslateMessage`/`handleHideTranslation` chữ ký khớp props `onTranslate`/`onHideTranslation`. Task 2 dùng `handleOpenProfile` đã là `useCallback` từ Task 1.
- **Memo hiệu lực:** props truyền xuống đều primitive (`msg`, `currentUsername`, `translation`, `isTranslating`) hoặc callback ổn định (`useCallback` deps rỗng/[username] — `username` không đổi trong vòng đời ChatView; callback mở-profile đọc `usersRef` nên không phụ thuộc state `users`). Khi AI stream, chỉ object `msg` của bong bóng đang stream đổi tham chiếu → chỉ bong bóng đó re-render.
- **Giữ hành vi:** style/class (`copilot-loading`, `fadeIn`) sao chép nguyên vẹn; auto-scroll (GĐ1) không đụng; lazy-load giữ animation Modal nhờ mount vĩnh viễn sau lần mở đầu.
- **Thứ tự task:** Task 1 thêm `useCallback` vào import React và biến `handleOpenProfile` thành useCallback; Task 2 mở rộng cùng dòng import (thêm `lazy, Suspense`) và cùng hàm `handleOpenProfile` (thêm `setProfileEverOpened`) — viết theo trạng thái sau Task 1, build xanh ở mỗi commit.
