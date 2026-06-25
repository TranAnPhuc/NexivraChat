# Kế hoạch sửa lỗi Schema Database và cập nhật context.md (Cập nhật)

Kế hoạch này thực hiện sửa lỗi lệch cột ở cả bảng `users` và `messages` trong cơ sở dữ liệu PostgreSQL local.

## Phát hiện mới về lỗi Schema
Khi kiểm tra cấu trúc bảng trong PostgreSQL, chúng tôi phát hiện nhiều cột đang bị lệch định dạng snake_case (thiếu dấu gạch dưới) do bảng cũ tồn tại từ trước:
- Bảng `users`: Có cột `createdat` (trong khi code yêu cầu `created_at`), thiếu cột `password_hash`.
- Bảng `messages`: Có các cột `roomid`, `sendername`, `createdat` (trong khi code yêu cầu `room_id`, `sender_name`, `created_at`) và thiếu cột `is_ai`.

## Proposed Changes

### Database Schema Updates
Chúng ta có 2 phương án giải quyết:

#### Phương án C.1: Thực hiện chuỗi ALTER TABLE để đổi tên và thêm cột (Giữ lại dữ liệu cũ)
Chạy các truy vấn SQL sau:
```sql
-- Sửa bảng users
ALTER TABLE users RENAME COLUMN createdat TO created_at;

-- Sửa bảng messages
ALTER TABLE messages RENAME COLUMN roomid TO room_id;
ALTER TABLE messages RENAME COLUMN sendername TO sender_name;
ALTER TABLE messages RENAME COLUMN createdat TO created_at;
ALTER TABLE messages ADD COLUMN IF NOT EXISTS is_ai BOOLEAN DEFAULT FALSE NOT NULL;
ALTER TABLE messages DROP CONSTRAINT IF EXISTS fk_messages_chat_rooms;
ALTER TABLE messages ADD CONSTRAINT fk_messages_chat_rooms FOREIGN KEY (room_id) REFERENCES chat_rooms(id) ON DELETE CASCADE;
```

#### Phương án C.2: DROP các bảng cũ và để DbInitializer tạo mới (Khuyên dùng - Nhanh & Sạch sẽ)
Chạy các truy vấn SQL sau:
```sql
DROP TABLE IF EXISTS messages CASCADE;
DROP TABLE IF EXISTS users CASCADE;
```
Sau đó khởi chạy lại ứng dụng để [DbInitializer.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Data/DbInitializer.cs) tự động tạo lại các bảng chuẩn 100%.

---

## Task Breakdown

### Task 1: Thực hiện sửa đổi Schema trong PostgreSQL
- **Agent**: `database-architect`
- **Skill**: `database-design`
- **Priority**: P0
- **Dependencies**: Không có
- **INPUT**:
  - Chuỗi kết nối: `Host=localhost;Database=postgres;Username=postgres;Password=Boggyroom24032005@`
  - Các lệnh SQL theo phương án người dùng chọn (C.1 hoặc C.2).
- **OUTPUT**: Cấu trúc các bảng `users` và `messages` khớp hoàn toàn với định nghĩa trong mã nguồn.
- **VERIFY**: Truy vấn `information_schema.columns` để xác nhận cấu trúc bảng.

### Task 2: Cập nhật tài liệu kiến trúc trong context.md
- **Agent**: `documentation-writer`
- **Skill**: `documentation-templates`
- **Priority**: P1
- **Dependencies**: Task 1
- **INPUT**: File [context.md](file:///d:/Vibe_Coding/NexivraChat/context.md)
- **OUTPUT**: Cập nhật mục schema và ghi chú về lỗi xung đột bảng cũ.
- **VERIFY**: Xem nội dung file [context.md](file:///d:/Vibe_Coding/NexivraChat/context.md).

### Task 3: Chạy và kiểm tra đăng ký tài khoản (Register API)
- **Agent**: `backend-specialist`
- **Skill**: `verify-changes`
- **Priority**: P1
- **Dependencies**: Task 1
- **INPUT**: Khởi chạy ứng dụng Backend.
- **OUTPUT**: Đăng ký và gửi tin nhắn thử nghiệm thành công không gặp lỗi PostgreSQL.
- **VERIFY**: Kiểm tra dữ liệu được chèn chính xác vào cơ sở dữ liệu.

---

## Phase X: Verification
- [x] Chạy các truy vấn xác minh cấu trúc cột của bảng `users` và `messages`
- [x] Chạy kiểm tra Lint & Build Backend
- [x] Xác nhận không còn lỗi đăng ký do lệch cột
- [x] Cập nhật tài liệu `context.md` hoàn tất

## ✅ PHASE X COMPLETE
- Lint: ✅ Pass
- Security: ✅ No critical issues
- Build: ✅ Success
- Date: 2026-06-24

