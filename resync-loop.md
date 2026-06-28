# Plan: Cải tiến logic Resync lặp afterId sau Reconnect

Kế hoạch điều chỉnh cơ chế resync trong `ChatView.tsx` để lặp kéo liên tục các lô tin nhắn bị bỏ lỡ khi mất mạng dài, tránh hiện tượng hụt tin nhắn.

## 📋 Mục tiêu
1. **Lặp kéo tin nhắn**: Thay vì chỉ gọi API `afterId` một lần duy nhất với `limit=50`, triển khai vòng lặp `while` liên tục cập nhật `afterId` theo ID lớn nhất trong lô vừa nhận cho tới khi lô nhận được có số lượng `< 50`.
2. **Hỗ trợ đầy đủ**: Áp dụng cho cả hai luồng phòng chat nhóm (Room) và chat riêng tư (Private Chat).
3. **Tối ưu hiệu năng & Dedupe**: Gộp tất cả các tin nhắn thu thập được và lọc trùng lặp bằng `Set` (O(n)) trước khi cập nhật DOM bằng `flushSync`.
4. **Cơ chế Fallback an toàn**: Giữ nguyên cơ chế fallback gọi `fetchMessageHistory` / `fetchPrivateMessageHistory` nếu gặp lỗi trong quá trình lặp.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Frontend Code Refactoring
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)**:
  - Viết lại callback `conn.onreconnected(async () => { ... })`.
  - Triển khai hàm async helper `fetchMissedMessages` sử dụng vòng lặp `while` với biến an toàn `safetyGuard < 20`.
  - Cập nhật state `messages` đồng bộ bằng `flushSync` và lọc trùng lặp bằng `Set`.

### Phase 2: Kiểm thử & Cập nhật Tài liệu
- **Biên dịch**: Run `npm run build` xác nhận 0 lỗi TypeScript.
- **Tài liệu & Todos**: Tick mục `#1` (và `#4` nếu ghép hợp lý) trong `TODOS.md`, cập nhật `context.md`.
- **Git Commit**: Commit với message `fix(resync): lặp afterId để không hụt tin khi mất mạng dài`.
