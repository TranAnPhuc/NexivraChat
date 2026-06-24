# Design Spec — Giao diện thân thiện (PipelinePro-style) + Light/Dark cho NexivraChat

**Ngày:** 2026-06-24
**Dự án:** NexivraChat — Real-time AI Chat Copilot
**Mục tiêu:** Thay phong cách "terminal/IT" hiện tại bằng giao diện thân thiện với người dùng phổ thông, dựa trên design system `pipelinepro-DESIGN.md`, hỗ trợ chuyển **light/dark**.

---

## 1. Bối cảnh & Mục tiêu

Giao diện hiện tại theo phong cách terminal: nền tối `#0a0f1d`, xanh lime `#a3e635`, font monospace, góc vuông 0px, chữ kiểu kỹ thuật (`AI_COPILOT_HUD`, `// NO_MESSAGES_YET`, `CHANNELS_SYS`). Người dùng muốn giao diện **gần gũi, dễ dùng cho mọi người**, lấy cảm hứng từ PipelinePro (SaaS hiện đại, nền sáng, bo góc mềm, đổ bóng nhẹ).

Hướng đã chốt khi brainstorm:
- **Primary = Teal `#0D9488`** (KHÔNG dùng indigo của PipelinePro vì dự án giữ luật **Purple Ban**). Phụ = cyan `#0891B2`, nhấn = cam `#F97316`.
- **Cả light + dark, có nút chuyển**, mặc định **light**.
- Bỏ phong cách terminal: font Inter/Outfit, bo góc mềm, chữ tiếng Việt thân thiện.

---

## 2. Global Constraints

- **Purple Ban**: tuyệt đối không dùng tím/indigo (kể cả `#4F46E5`). Primary là teal `#0D9488`.
- Frontend: React 19 + TypeScript + antd + Vite (giữ stack hiện tại).
- Không đổi backend, không đổi schema DB, không đổi hợp đồng SignalR/API.
- Mặc định theme = `light`; lựa chọn lưu `localStorage` key `nexivra-theme`.
- Mọi màu trong inline style của component phải tham chiếu **CSS variable token**, không hardcode hex (trừ định nghĩa token).
- Font: **Outfit** cho tiêu đề, **Inter** cho nội dung; bỏ monospace.

---

## 3. Bảng màu & Token

Primary teal `#0D9488` (hover `#0F766E`, soft-bg `#F0FDFA`/dark `#134E4A`). Secondary cyan `#0891B2`. Accent cam `#F97316`. Semantic: success `#22C55E`, warning `#F59E0B`, error `#EF4444`.

Token CSS variable định nghĩa cho **cả 2 theme** (giá trị light / dark):

| Token | Light | Dark |
|---|---|---|
| `--bg-canvas` | `#F8FAFC` | `#0B1220` |
| `--bg-surface` | `#FFFFFF` | `#111827` |
| `--bg-elevated` | `#F1F5F9` | `#1E293B` |
| `--border` | `#E2E8F0` | `#1E293B` |
| `--text-primary` | `#0F172A` | `#F1F5F9` |
| `--text-secondary` | `#475569` | `#94A3B8` |
| `--text-muted` | `#94A3B8` | `#64748B` |
| `--primary` | `#0D9488` | `#14B8A6` |
| `--primary-hover` | `#0F766E` | `#0D9488` |
| `--primary-soft` | `#F0FDFA` | `#134E4A` |
| `--primary-on` | `#FFFFFF` | `#FFFFFF` |
| `--secondary` | `#0891B2` | `#22D3EE` |
| `--accent` | `#F97316` | `#FB923C` |
| `--bubble-me` | `#0D9488` (chữ trắng) | `#0D9488` |
| `--bubble-me-text` | `#FFFFFF` | `#FFFFFF` |
| `--bubble-other` | `#FFFFFF` | `#1E293B` |
| `--bubble-other-text` | `#0F172A` | `#F1F5F9` |
| `--bubble-ai-bg` | `#F0FDFA` | `#134E4A` |
| `--bubble-ai-border` | `#99F6E4` | `#0D9488` |
| `--bubble-ai-text` | `#134E4A` | `#CCFBF1` |

Bo góc: input/nút/thẻ `8px`, bong bóng tin nhắn `14px` (góc nhọn nhẹ phía người gửi), avatar `50%`.

---

## 4. Kiến trúc theming

1. **Tokens** trong `index.css`: `:root[data-theme="light"] { --… }` và `:root[data-theme="dark"] { --… }`. `body` đặt `background: var(--bg-canvas)`, `color: var(--text-primary)`, `font-family: 'Inter', system-ui`.
2. **ThemeContext** (`src/theme/ThemeContext.tsx`): cung cấp `{ theme: 'light'|'dark', toggleTheme() }`. Khi đổi → `localStorage.setItem('nexivra-theme', theme)` và `document.documentElement.setAttribute('data-theme', theme)`. Hàm thuần `getInitialTheme(): 'light'|'dark'` đọc localStorage, fallback `'light'` (bọc try/catch nếu localStorage không khả dụng).
3. **Chống FOUC**: trong `main.tsx`, gọi `document.documentElement.setAttribute('data-theme', getInitialTheme())` **trước** `createRoot(...).render(...)`.
4. **antd ConfigProvider** (trong `App.tsx`): import algorithm từ antd để tránh trùng tên biến — `import { theme as antdTheme } from 'antd'`, rồi `<ConfigProvider theme={{ algorithm: currentTheme === 'dark' ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm, token: { colorPrimary: '#0D9488', borderRadius: 8, fontFamily: "'Inter', system-ui, sans-serif" } }}>` (với `currentTheme` lấy từ `useTheme()`).
5. **ThemeToggle** (`src/components/ThemeToggle.tsx`): nút icon mặt trời/mặt trăng (antd `Button` type text + `@ant-design/icons` `SunOutlined`/`MoonOutlined` hoặc tương đương), gọi `toggleTheme()`. Đặt ở header phòng chat (góc phải) và ở `LoginView`.

---

## 5. Phạm vi & thay đổi từng màn hình

### 5.1. Files mới
- `src/theme/ThemeContext.tsx` — provider + `useTheme()` hook + `getInitialTheme()`.
- `src/components/ThemeToggle.tsx` — nút chuyển theme.

### 5.2. Files sửa
- `index.html` — nạp Google Fonts: Outfit (400/500/700) + Inter (400/500/600).
- `src/main.tsx` — set `data-theme` trước render.
- `src/index.css` — token 2 theme, font nền, scrollbar mềm (bỏ scrollbar "console"), bỏ style monospace toàn cục.
- `src/App.tsx` — bọc `<ThemeProvider>` + antd `<ConfigProvider>`; truyền theme.
- `src/views/LoginView.tsx` — bố cục thẻ trắng/teal nền sáng, font Inter/Outfit, chữ tiếng Việt; có ThemeToggle.
- `src/views/ChatView.tsx` — header (tên phòng + "N người đang online" + ThemeToggle), vùng tin nhắn nền canvas, bong bóng bo tròn (me=teal, other=surface viền, AI=teal-soft có icon robot), typing "X đang gõ…", ô nhập + nút "Gửi". Tất cả màu → token.
- `src/components/RoomSidebar.tsx` — tiêu đề "Phòng chat", danh sách phòng kiểu list item (active: nền teal-soft + viền trái teal), khối user dưới cùng với avatar chữ cái.
- `src/components/CopilotPanel.tsx` — tiêu đề "Trợ lý AI", các thẻ hành động bo góc mềm, nút teal viền, chữ tiếng Việt.

### 5.3. Viết lại nội dung (terminal → tiếng Việt)

| Hiện tại | Mới |
|---|---|
| `CHANNELS_SYS` | Phòng chat |
| `#NO_ROOM_SELECTED` / `NO_ROOM_SELECTED` | Chưa chọn phòng |
| `// NO_MESSAGES_YET - START_THE_CONVERSATION` | Chưa có tin nhắn — hãy bắt đầu trò chuyện 👋 |
| `AI_COPILOT_HUD` | Trợ lý AI |
| `RUN_SUMMARIZE` | Tóm tắt phòng |
| `BRAINSTORM_TOPICS` | Gợi ý chủ đề |
| `CONNECTED_AS` | Đang đăng nhập |
| `SYSTEM_ACTIVE v1.0.0` | NexivraChat |
| `Type a message... (Use @copilot to query AI Assistant)` | Nhập tin nhắn… (gõ @copilot để hỏi AI) |
| `SEND` | Gửi |

Các chuỗi đã thân thiện sẵn (vd `Copilot đang phản hồi...`, thông báo tham gia/rời phòng) giữ nguyên hoặc tinh chỉnh nhẹ cho nhất quán giọng văn.

---

## 6. Xử lý lỗi & edge case
- `localStorage` không khả dụng → `getInitialTheme()` trả `'light'`, app vẫn chạy.
- Theme áp dụng trước render → không nhấp nháy khi tải lại.
- Đổi theme khi đang chat: chỉ đổi token + algorithm antd, **không** ảnh hưởng kết nối SignalR hay state tin nhắn.

---

## 7. Kiểm thử
Frontend chưa có test runner → xác minh chủ yếu thủ công:
1. `npx tsc --noEmit` và `npm run build` đều sạch.
2. Rà **cả light và dark** trên 4 màn hình: Login, danh sách phòng, khung chat (bong bóng me/other/AI + typing + online count), panel Trợ lý AI.
3. Nút chuyển theme đổi tức thì, reload giữ đúng theme đã chọn (localStorage).
4. Không còn màu **tím/indigo**; không còn chữ "terminal"; không còn font monospace.
5. Luồng chat realtime + `@copilot` vẫn hoạt động (không bị ảnh hưởng bởi thay đổi giao diện).

---

## 8. Out of scope (YAGNI)
- Tùy biến nhiều theme/màu ngoài light/dark.
- Theo theme hệ điều hành (`prefers-color-scheme`) — chỉ cần mặc định light + nút chuyển thủ công.
- Thiết lập test runner frontend (vitest) — để đợt sau nếu cần.
- Thay đổi tính năng/chức năng; đợt này thuần giao diện + nội dung.

---

## 9. Tiêu chí thành công
- Giao diện 4 màn hình theo phong cách PipelinePro (đã đổi teal), nền sáng mặc định, có dark mode mượt.
- Toàn bộ chữ tiếng Việt thân thiện, không còn thuật ngữ terminal/monospace.
- Không dùng tím; build + typecheck sạch; chat/AI hoạt động bình thường.
