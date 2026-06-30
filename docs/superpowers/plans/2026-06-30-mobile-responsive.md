# Kế Hoạch: Tối Ưu Giao Diện Mobile (Responsive)

> Tạo qua phiên review với Claude ngày 2026-06-30 · Mô hình: **Claude lập kế hoạch → Antigravity 2.0 viết code**. File này là bản giao cho Antigravity.

---

## 1. Mục Tiêu & Bối Cảnh

**Vấn đề:** UI hiện tại là **desktop-only 3 cột cứng**. Gốc `ChatView` (`views/ChatView.tsx:1125`) là flex ngang `width: 100vw; overflow: hidden` xếp cạnh nhau: **`RoomSidebar` (1126) | khung chat flex:1 (1143) | `CopilotPanel` (1443)**. Mọi style inline bằng px cố định, không co lại. Trên điện thoại (~375px) sidebar + copilot ăn hết bề ngang, khung chat bị bóp gần như không dùng được.

**Hiện trạng responsive:** gần như **chưa có**. `App.css` có `@media` nhưng là **CSS rác của template Vite** (`.hero`, `#docs`, `#next-steps`) — không liên quan UI chat. `index.html` đã có meta viewport (app scale được), `ProfileView` có vài media query hover lẻ.

**Mục tiêu:** App dùng tốt trên điện thoại (khung chat full-width, không tràn ngang, chạm tay dễ) **mà KHÔNG phá vỡ giao diện desktop hiện có**.

---

## 2. Quyết Định Đã Chốt (KHÔNG được đổi khi code)

| # | Quyết định | Giá trị chốt |
|---|-----------|--------------|
| D1 | Layout mobile | **Drawer trượt**: khung chat full-width; `RoomSidebar` + `CopilotPanel` thành antd `Drawer`, mở bằng nút ở header. Chọn phòng/hội thoại xong → drawer tự đóng. |
| D2 | Breakpoint | `< 768px` = mobile (drawer); `>= 768px` = giữ nguyên 3 cột desktop như hiện tại |
| D3 | Phát hiện mobile | Hook `useIsMobile()` (JS, `window.innerWidth` + listener `resize`) — vì UI dùng inline style, conditional render sạch hơn pure-CSS, và antd Drawer vốn điều khiển bằng JS |
| D4 | Phạm vi giữ desktop | Nhánh desktop (`>=768px`) phải render **y hệt hiện tại** — chỉ thêm nhánh mobile, không sửa hành vi desktop |

---

## 3. Cách Tiếp Cận

Thêm một nhánh render mobile, **không động vào nhánh desktop**:

```
ChatView render
   │
   ├─ isMobile === false  ──▶  Layout 3 cột HIỆN TẠI (giữ nguyên 100%)
   │
   └─ isMobile === true   ──▶  Layout mobile:
          • Khung chat: full-width (width 100%, không 100vw cứng)
          • RoomSidebar: bọc trong <Drawer placement="left">, toggle bằng nút ☰ ở header
          • CopilotPanel: bọc trong <Drawer placement="right"> (hoặc bottom), toggle bằng nút AI ở header
          • Chọn phòng/user trong sidebar → đóng drawer (onClose)
          • Các panel absolute (search, mention) → clamp width vừa màn hình
```

**Nguyên tắc:** dùng cùng state/logic chat đã có; chỉ khác cách **bố trí** 3 khối. Không tách logic, không trùng code (DRY) — RoomSidebar/CopilotPanel vẫn là component cũ, chỉ khác chỗ đặt (trong Drawer hay inline).

---

## 4. Các File Cần Tạo / Sửa

### 4.1 `src/hooks/useIsMobile.ts` (TẠO MỚI)
```ts
// Trả về true khi viewport < breakpoint (mặc định 768). Lắng nghe resize.
export function useIsMobile(breakpoint = 768): boolean { ... }
```
- Dùng `window.matchMedia(`(max-width: ${breakpoint - 1}px)`)` + listener; cleanup khi unmount.
- SSR-safe không cần (Vite SPA), nhưng khởi tạo từ `window.innerWidth` để tránh nháy.

### 4.2 `src/views/ChatView.tsx` (SỬA — phần render layout, ~dòng 1124-1443)
- Gọi `const isMobile = useIsMobile();`.
- Thêm 2 state: `sidebarOpen`, `copilotOpen` (chỉ dùng khi mobile).
- **Root container:** khi mobile, đổi `width: '100vw'` → `width: '100%'`, và `height: '100vh'` → `height: '100dvh'` (xem 4.6). Khung chat con đặt `width: 100%`.
- **Header khung chat (dòng ~1145):** khi mobile, thêm bên trái nút ☰ (mở `sidebarOpen`); bên phải, khi `activeChatType === 'room'`, thêm nút AI (mở `copilotOpen`). Hai nút này `display: none` trên desktop (chỉ render khi `isMobile`).
- **RoomSidebar (dòng 1126):**
  - Desktop: render inline như cũ.
  - Mobile: render trong `<Drawer placement="left" open={sidebarOpen} onClose={...} width="82%" bodyStyle={{padding:0}}>`. Truyền callback chọn phòng/user đã có, **bọc thêm** `() => { ...chọn...; setSidebarOpen(false); }` để tự đóng drawer sau khi chọn.
- **CopilotPanel (dòng 1443):**
  - Desktop: `{activeChatType === 'room' && <CopilotPanel .../>}` như cũ.
  - Mobile: render trong `<Drawer placement="right" open={copilotOpen} onClose={...} width="85%">` (chỉ khi room).
- **Panel absolute search (dòng ~1205, `width: 320px`):** đổi thành `width: 'min(320px, calc(100vw - 24px))'` và đảm bảo không tràn (canh `left`/`right` an toàn trên mobile).
- **Dropdown mention (dòng ~1365, `width: 220px`):** tương tự clamp `min(220px, calc(100vw - 32px))`.

### 4.3 `src/components/RoomSidebar.tsx` (SỬA nhẹ)
- Bỏ width cố định bao ngoài KHI ở trong Drawer (Drawer tự set width). Cách an toàn: nhận prop tuỳ chọn `fullWidth?: boolean`; khi true thì container dùng `width: 100%` thay vì px cố định. ChatView truyền `fullWidth={isMobile}`.
- Đảm bảo danh sách cuộn được trong Drawer (`height: 100%`, `overflowY: auto`).

### 4.4 `src/components/CopilotPanel.tsx` (SỬA nhẹ)
- Tương tự: trong Drawer dùng `width: 100%`; nội dung cuộn được.

### 4.5 Chạm tay & cỡ chữ (toàn ChatView + MessageBubble)
- Nút bấm/icon tương tác (dịch, reply, edit, react, gửi) đảm bảo vùng chạm **≥ 44×44px** trên mobile.
- Ô nhập tin: `font-size: 16px` trên mobile (tránh iOS auto-zoom khi focus input < 16px).
- Padding khung chat: giảm từ 20px → ~12px trên mobile để tận dụng bề ngang.

### 4.6 Vá lỗi `100vh` trên mobile (toàn cục)
- Thay `height: 100vh` ở root bằng `100dvh` (dynamic viewport height) để không bị thanh địa chỉ trình duyệt che mất ô nhập. Fallback: thêm `height: 100vh` trước `100dvh` cho trình duyệt cũ, hoặc dùng biến CSS.
- Đảm bảo `overflow-x: hidden` ở `body`/root để tuyệt đối không có cuộn ngang.

### 4.7 `LoginView.tsx` (SỬA — kiểm tra responsive)
- Form đăng nhập/đăng ký: đảm bảo padding co lại, card không tràn, input full-width trên mobile. Thường nhẹ.

### 4.8 `ProfileView.tsx` (SỬA — kiểm tra modal trên mobile)
- Modal hồ sơ: trên mobile dùng `width: '100%'` (hoặc `style={{ top: 0 }}` full-screen), tag sở thích / social wrap đúng, avatar + grid chỉ số không tràn.

### 4.9 `App.css` (DỌN DẸP)
- Xoá toàn bộ CSS rác template Vite (`.hero`, `#center`, `#next-steps`, `#docs`, `#spacer`, `.ticks`, `.counter`) — không nơi nào dùng. Giảm nhiễu, tránh hiểu nhầm là responsive thật.

---

## 5. Kiểm Thử / Nghiệm Thu (thủ công — đây là việc UI)

Test trên DevTools responsive + nếu được thì điện thoại thật:

| Màn | Kiểm tra |
|-----|---------|
| ChatView | Khung chat full-width, KHÔNG cuộn ngang ở 360/375/414px. Nút ☰ mở sidebar drawer; chọn phòng → drawer đóng + vào đúng phòng. Nút AI mở copilot drawer. |
| Gửi tin | Ô nhập không bị thanh địa chỉ che (test cuộn lên xuống trên mobile thật). Focus input KHÔNG auto-zoom (font 16px). |
| Search / mention | Panel/dropdown không tràn mép phải màn hình. |
| Login | Form gọn, không tràn, nút full-width. |
| Profile | Modal vừa màn hình, nội dung không tràn. |
| Desktop (>=768px) | **Không đổi gì** so với hiện tại — regression check quan trọng nhất. |
| Light + Dark | Cả 2 theme đều đúng trên mobile. |

> **Lưu ý:** đây là phần frontend tĩnh → deploy qua Cloudflare Pages (push `main` auto-deploy). Không đụng backend, không migration.

---

## 6. NOT In Scope (hoãn)

| Hạng mục | Lý do |
|---------|------|
| Thêm thư viện responsive (vd react-responsive) | `useIsMobile` tự viết là đủ, tránh thêm dependency |
| Bottom-tab navigation toàn app | Đã chọn drawer (D1); tab là hướng khác |
| PWA / cài lên màn hình chính | Ngoài phạm vi tối ưu hiển thị |
| Gesture vuốt để mở/đóng drawer | antd Drawer mở bằng nút là đủ cho MVP; vuốt để sau |
| Tối ưu ảnh/bandwidth cho mạng yếu | Là việc hiệu năng khác, không phải layout |

---

## 7. What Already Exists (tái dùng)

- `RoomSidebar`, `CopilotPanel`, `MessageBubble` — giữ nguyên component, chỉ đổi nơi đặt + thêm prop `fullWidth`.
- antd `Drawer` — đã có antd trong dự án, không thêm dependency.
- CSS variables trong `index.css` (`--bg-*`, `--border`, `--primary`) — dùng tiếp cho style mobile.
- Toàn bộ state/logic chat trong ChatView — không sửa, chỉ đổi bố trí render.

---

## 8. Implementation Tasks (giao Antigravity — checkbox khi xong)

- [x] **T1 (P1)** — Tạo `src/hooks/useIsMobile.ts` (matchMedia + resize listener, breakpoint 768).
- [x] **T2 (P1)** — ChatView: thêm `isMobile` + state `sidebarOpen`/`copilotOpen`; root container dùng `width:100%` + `100dvh` khi mobile; `overflow-x:hidden`.
  - Verify: ở 375px khung chat chiếm full-width, không cuộn ngang.
- [x] **T3 (P1)** — ChatView: bọc `RoomSidebar` trong `<Drawer left>` khi mobile + nút ☰ ở header; chọn phòng/user → đóng drawer.
- [x] **T4 (P1)** — ChatView: bọc `CopilotPanel` trong `<Drawer right>` khi mobile (chỉ room) + nút AI ở header.
- [x] **T5 (P1)** — RoomSidebar + CopilotPanel: prop `fullWidth`, container `width:100%` khi trong drawer, nội dung cuộn được.
- [x] **T6 (P2)** — Clamp panel absolute: search `min(320px, calc(100vw-24px))`, mention `min(220px, calc(100vw-32px))`.
- [x] **T7 (P2)** — Chạm tay: nút tương tác ≥44px, ô nhập `font-size:16px`, giảm padding khung chat trên mobile.
- [x] **T8 (P2)** — LoginView responsive (form gọn, input full-width).
- [x] **T9 (P2)** — ProfileView: modal full-width/full-screen trên mobile, nội dung không tràn.
- [x] **T10 (P3)** — Xoá CSS rác template Vite trong `App.css`.
- [x] **T11 (P1)** — Regression desktop: xác nhận `>=768px` render y hệt trước (so sánh trực quan).

P1 = cốt lõi mobile · P2 = polish · P3 = dọn dẹp.

---

## 9. Sau Khi Xong
1. Test responsive theo Mục 5 (mobile + **regression desktop**).
2. `git push main` → Cloudflare Pages auto-deploy.
3. Mở https://nexivrachat.pages.dev trên điện thoại thật kiểm tra lần cuối.
