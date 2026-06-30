# Kế Hoạch: Tối Ưu Trải Nghiệm & Độ Tin Cậy Chat

> Tạo qua phiên review với Claude ngày 2026-06-30 · Mô hình: **Claude lập kế hoạch → Antigravity 2.0 viết code**. File này là bản giao cho Antigravity.

---

## 1. Mục Tiêu & Bối Cảnh

Tối ưu **độ tin cậy & cảm nhận** của các tính năng chat đang có, không thêm tính năng mới. Trọng tâm: app phải "đáng tin" ngay cả khi mạng chập chờn và khi **Render free cold-start ~50s**.

**Audit hiện trạng (`views/ChatView.tsx`):**
- ✅ Đã có: toast lỗi API khắp nơi; `withAutomaticReconnect()` + `onreconnected` resync tinh vi; gửi thất bại giữ lại draft (input chỉ xóa khi thành công, dòng 1060).
- 🔴 **Lỗ hổng 1 — không phản hồi khi MẤT kết nối:** có `onreconnected` nhưng KHÔNG có `onreconnecting`/`onclose` → lúc rớt mạng người dùng không thấy gì, vẫn gõ/gửi vào kết nối chết.
- 🔴 **Lỗ hổng 2 — gửi tin không optimistic:** `handleSendMessage` (1049) `await invoke()` rồi chờ server dội lại. Cold-start 50s → bấm gửi đứng hình tới 50s, nút gửi không quay.
- 🟡 **Lỗ hổng 3 — không phân biệt "đang tải" và "rỗng":** không có cờ loading lịch sử lần đầu → đang tải vẫn hiện empty-state "chưa có tin nhắn".
- 🟡 **Lỗ hổng 4 — toast lỗi spam** khi rớt mạng dồn dập (không key dedupe).

---

## 2. Quyết Định Đã Chốt (KHÔNG được đổi khi code)

| # | Quyết định | Giá trị chốt |
|---|-----------|--------------|
| D1 | Trạng thái kết nối | 3 trạng thái `connecting` / `reconnecting` / `disconnected` → banner mỏng trên cùng vùng chat khi != connected |
| D2 | Optimistic send | Tin hiện NGAY dạng `sending`; ghép với tin thật qua **`clientId`** do backend dội lại (tránh nhân đôi) → **cần sửa nhẹ ChatHub + Message** |
| D3 | Gửi thất bại | Bong bóng hiện trạng thái `failed` + nút "Gửi lại"; retry tái dùng cùng `clientId` |
| D4 | Loading lần đầu | Cờ `loadingHistory` → spinner/skeleton, phân biệt rõ với empty-state |
| D5 | Phạm vi giữ nguyên | Không đổi logic resync/pagination/AI-stream đã chạy tốt; chỉ thêm lớp UX/độ tin cậy |

---

## 3. Cách Hoạt Động (Optimistic Send + Reconcile)

```
Người dùng bấm Gửi
   │
   ├─ 1. Sinh clientId (vd crypto.randomUUID)
   ├─ 2. setMessages: thêm NGAY bong bóng {clientId, content, status:'sending', senderName=me}
   ├─ 3. Xóa input ngay (lạc quan)
   ├─ 4. await connection.invoke('SendMessage', ..., clientId)
   │        ├─ OK   → KHÔNG làm gì thêm; chờ server dội ReceiveMessage (mang clientId)
   │        └─ Lỗi  → cập nhật bong bóng đó status:'failed' (hiện nút "Gửi lại")
   │
   ▼
Server lưu DB → broadcast ReceiveMessage (payload có clientId)
   │
   ▼
Frontend nhận ReceiveMessage:
   ├─ Nếu payload.clientId trùng 1 bong bóng 'sending' của mình
   │     → THAY THẾ nó (gán id thật, status:'sent') — KHÔNG thêm mới (hết nhân đôi)
   └─ Ngược lại (tin người khác) → thêm mới như cũ
```

Vì hiện tại server đã dội `ReceiveMessage` cho cả người gửi (Clients.Group/Users gồm cả sender), chỉ cần thêm `clientId` vào payload là ghép được.

---

## 4. Backend — Sửa Nhẹ (để optimistic send ghép đúng)

### 4.1 `Models/Message.cs`
- Thêm `public string? ClientId { get; set; }` — **KHÔNG lưu DB**, chỉ truyền qua broadcast. Repositories dùng cột tường minh nên thuộc tính thừa này không bị insert (an toàn). Nếu cẩn thận có thể đánh dấu `[Dapper.Contrib]`/bỏ qua, nhưng do SQL tường minh nên không cần.

### 4.2 `Hubs/ChatHub.cs`
- `SendMessage(... , string? clientId = null)`: sau `SaveNewMessage`, gán `userMessage.ClientId = clientId;` TRƯỚC khi `Clients.Group(...).SendAsync("ReceiveMessage", userMessage)`.
- `SendPrivateMessage(..., string? clientId = null)`: tương tự, gán trước `SendAsync("ReceivePrivateMessage", userMessage)`.
- **Thứ tự tham số:** thêm `clientId` vào CUỐI danh sách tham số (sau `attachmentSize`) để không vỡ các lệnh gọi cũ.
- Moderation đã chạy trước đó vẫn nguyên (không đụng).

> Backend change này nhỏ nhưng nghĩa là deploy đợt này chạm **cả Render (backend) lẫn Cloudflare (frontend)**. Không có migration DB.

---

## 5. Frontend — Các File Cần Sửa (`views/ChatView.tsx` là chính)

### 5.1 Kiểu `Message` (nơi khai báo interface)
- Thêm `clientId?: string` và `status?: 'sending' | 'sent' | 'failed'`.

### 5.2 Trạng thái kết nối (D1)
- Thêm state `connectionStatus: 'connecting' | 'connected' | 'reconnecting' | 'disconnected'`.
- Wire vào connection (đoạn ~455-810):
  - khởi tạo `connecting`; `conn.start().then` → `connected`; `.catch` → `disconnected`.
  - `conn.onreconnecting(() => setConnectionStatus('reconnecting'))`.
  - `conn.onreconnected(...)` (đã có) → thêm `setConnectionStatus('connected')`.
  - `conn.onclose(() => setConnectionStatus('disconnected'))`.
- **Banner** (đầu vùng chat, dưới header): hiện khi `!= 'connected'`:
  - `connecting` (lần đầu, có thể là cold-start): "Đang kết nối máy chủ… (lần đầu có thể mất ~50 giây)".
  - `reconnecting`: "Đang kết nối lại…" (nền hổ phách).
  - `disconnected`: "Mất kết nối — đang thử lại" (nền đỏ dịu).

### 5.3 Optimistic send (D2, D3) — sửa `handleSendMessage` (1036)
- Sinh `clientId = crypto.randomUUID()`.
- Trước `invoke`: thêm bong bóng optimistic vào `messages` (status `sending`, clientId, content, senderName = chính mình, createdAt = now, id = số âm tạm để React key — dùng pattern temp-id âm sẵn có hoặc clientId làm key).
- Xóa input + reply + attachment NGAY (lạc quan) thay vì chỉ khi thành công.
- `invoke('SendMessage'|'SendPrivateMessage', ..., clientId)`.
- `catch`: tìm bong bóng theo `clientId`, set `status:'failed'`. Giữ logic mute cũ (regex `Bạn đang bị tạm hạn chế…`). Nếu là lỗi mute/kiểm duyệt (HubException) → cũng đánh dấu failed + vẫn show toast.
- **Guard:** nếu `connectionStatus !== 'connected'`, không cho gửi (hoặc set failed ngay) + hint "Mất kết nối, thử lại sau".

### 5.4 Reconcile khi nhận tin (sửa handler `ReceiveMessage` + `ReceivePrivateMessage`)
- Khi nhận payload: nếu `payload.clientId` tồn tại và trùng một bong bóng đang `sending` của mình → **map thay thế** (gán id thật, status `sent`, xóa cờ optimistic) thay vì `[...prev, payload]`.
- Ngược lại giữ nguyên hành vi thêm mới (dedup theo id như đang có).

### 5.5 Retry (D3) — `MessageBubble.tsx`
- Bong bóng `status:'failed'` (tin của mình): hiện nhãn đỏ "Gửi lỗi" + nút "Gửi lại".
- Bong bóng `status:'sending'`: hiện đồng hồ/"đang gửi…" mờ + tick xám.
- Prop mới: `onRetry?(clientId)`; ChatView truyền handler retry → set lại `sending` + `invoke` lại cùng clientId + content.

### 5.6 Loading lần đầu (D4)
- Thêm state `loadingHistory`; bật true đầu `fetchMessageHistory`/`fetchPrivateMessageHistory`, tắt ở `finally`.
- Vùng tin: khi `loadingHistory && messages.length === 0` → hiện `<Spin>`/skeleton "Đang tải tin nhắn…" THAY cho empty-state. Chỉ hiện "Chưa có tin nhắn" khi `!loadingHistory && messages.length === 0`.

### 5.7 Nút gửi + dedupe toast (lỗ hổng 4)
- Nút Gửi: thêm trạng thái `loading` ngắn trong lúc `invoke` đang chạy (hoặc bỏ qua nếu đã có optimistic — optimistic làm phản hồi tức thì rồi). Giữ disabled khi `mutedUntilText`.
- Toast lỗi kết nối: dùng `message.error({ content, key: 'conn-error' })` để lần lỗi sau THAY lần trước (hết spam).

### 5.8 (Tùy chọn nhỏ) Trạng thái loading danh sách phòng
- Nếu dễ, RoomSidebar hiện skeleton lúc tải rooms lần đầu. Ưu tiên thấp.

---

## 6. Kiểm Thử / Nghiệm Thu (thủ công)

| Tình huống | Kỳ vọng |
|-----------|---------|
| Gửi tin (mạng tốt) | Bong bóng hiện NGAY (sending), đổi sang sent khi server dội về, **không nhân đôi** |
| Gửi tin lúc cold-start (chờ server ngủ dậy) | Hiện banner "đang kết nối…", tin ở trạng thái sending, không đứng hình; xong thì thành sent |
| Gửi khi mất mạng | Tin thành failed + nút "Gửi lại"; bấm gửi lại → sent khi có mạng |
| Rớt mạng giữa chừng | Banner "đang kết nối lại…" hiện; nối lại thì banner mất + resync tin (giữ logic cũ) |
| Mở phòng nhiều tin | Hiện "đang tải tin nhắn…" rồi ra tin, KHÔNG nháy "chưa có tin nhắn" |
| Phòng thật sự rỗng | Hiện "chưa có tin nhắn" (chỉ sau khi tải xong) |
| DM + nhóm | Cả hai đều optimistic + reconcile đúng |
| Desktop + mobile, light + dark | Banner + trạng thái hiển thị đúng |

---

## 7. NOT In Scope (hoãn)

| Hạng mục | Lý do |
|---------|------|
| Hàng đợi gửi offline (gửi lại tự động khi có mạng) | Phức tạp; MVP để người dùng bấm "Gửi lại" thủ công |
| Optimistic cho edit/delete/reaction | Tập trung gửi tin trước; các cái kia đã có toast lỗi |
| Service worker / offline cache | Ngoài phạm vi độ tin cậy realtime |
| Đổi `withAutomaticReconnect` sang cấu hình backoff tùy chỉnh | Mặc định đang ổn |
| Optimistic cho AI copilot stream | Luồng AI đã có placeholder + temp-id riêng |

---

## 8. What Already Exists (tái dùng)

- Pattern **temp-id âm** cho tin AI (`ReceiveAiToken`/`ReceiveAiComplete`) — tái dùng ý tưởng cho id tạm của optimistic message.
- `message.error` / `notification` (antd) — đã import sẵn.
- `onreconnected` resync (dòng 670) — giữ nguyên, chỉ thêm set trạng thái.
- Logic dedup theo id trong `setMessages` — mở rộng để dedup theo clientId.
- `MessageBubble` (đã `React.memo`) — thêm prop status/onRetry, không phá tối ưu render.

---

## 9. Implementation Tasks (giao Antigravity — checkbox khi xong)

**Backend:**
- [x] **T1 (P1)** — `Message.cs`: thêm `ClientId` (không lưu DB).
- [x] **T2 (P1)** — `ChatHub.cs`: `SendMessage`/`SendPrivateMessage` nhận `clientId` (cuối params), gán vào object trước broadcast.

**Frontend:**
- [x] **T3 (P1)** — Kiểu `Message`: thêm `clientId?`, `status?`.
- [x] **T4 (P1)** — Trạng thái kết nối + banner (onreconnecting/onclose/onreconnected/start).
- [x] **T5 (P1)** — `handleSendMessage` optimistic: thêm bong bóng sending, xóa input ngay, clientId, catch→failed, guard khi mất kết nối.
- [x] **T6 (P1)** — Reconcile `ReceiveMessage`/`ReceivePrivateMessage` theo clientId (thay thế, không nhân đôi).
- [x] **T7 (P1)** — `MessageBubble`: render trạng thái sending/failed + nút "Gửi lại" (prop onRetry); ChatView nối handler retry.
- [x] **T8 (P2)** — `loadingHistory` + spinner phân biệt empty-state.
- [x] **T9 (P2)** — Nút gửi loading + dedupe toast lỗi kết nối (key).
- [x] **T10 (P3)** — (Tùy chọn) Bỏ qua (không cần thiết vì load phòng tức thời).

P1 = cốt lõi độ tin cậy · P2 = polish · P3 = nice-to-have.

---

## 10. Sau Khi Xong
1. `dotnet build` backend (T1-T2) + `npm run build` frontend đều xanh.
2. Test thủ công Mục 6 (đặc biệt: **không nhân đôi tin** sau khi server dội về, và cold-start không đứng hình).
3. Commit + push `main` → deploy **cả Render lẫn Cloudflare** (không migration DB).
