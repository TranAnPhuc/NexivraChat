# Plan: Real-time Chat Completion with AI Fallback

Kế hoạch hoàn thiện ứng dụng AI Chat Realtime MVP dựa trên Option A (Cài đặt PostgreSQL tự động qua Docker + Gemini API thực tế/Mock fallback).

---

## 📌 Project Overview
- **Dự án:** AI Chat Realtime MVP
- **Kiểu dự án:** WEB (ASP.NET Core Web API + React TS)
- **Mục tiêu:**
  1. Tự động hóa thiết lập cơ sở dữ liệu PostgreSQL qua Docker Compose.
  2. Khắc phục lỗi biên dịch giao diện Frontend (`CopilotPanel.tsx`).
  3. Cài đặt đầy đủ các thư viện phụ thuộc và khởi động hệ thống.
  4. Hỗ trợ nạp Gemini API Key qua cả cấu hình `appsettings.json` và biến môi trường `GEMINI_API_KEY` với cơ chế tự động fallback sang Mock AI nếu không tìm thấy key.

---

## 🏆 Success Criteria
- [ ] Khởi chạy PostgreSQL thành công qua Docker Compose mà không cần cài đặt thủ công.
- [ ] Build Frontend thành công không gặp lỗi biên dịch TypeScript.
- [ ] Gửi và nhận tin nhắn thời gian thực qua SignalR hoạt động trơn tru trên 2 cửa sổ trình duyệt khác nhau.
- [ ] Nhập `@copilot` kích hoạt phản hồi stream của AI thực tế (nếu có key) hoặc mock text chạy từ từ (nếu thiếu key) mà không gây crash hệ thống.
- [ ] Tuân thủ triệt để Quy tắc thiết kế (không sử dụng màu tím - Purple Ban).

---

## 🛠️ Tech Stack & Database Connection
- **Backend:** .NET 8 (SignalR Hub + Dapper)
- **Frontend:** React 19 + Ant Design (antd) + Tailwind CSS (v4)
- **Database:** PostgreSQL (chạy cổng 5432 trên Docker)
- **ConnectionString:** `Host=localhost;Database=postgres;Username=postgres;Password=Boggyroom24032005@`

---

## 📁 Proposed File Structure

Các file chính được sửa đổi hoặc tạo mới:
- `[NEW]` [docker-compose.yml](file:///d:/Desktop/NexivraChat/docker-compose.yml) (Root)
- `[MODIFY]` [CopilotPanel.tsx](file:///d:/Desktop/NexivraChat/frontend/nexivra-chat-frontend/src/components/CopilotPanel.tsx) (Frontend)
- `[MODIFY]` [appsettings.json](file:///d:/Desktop/NexivraChat/backend/NexivraChatBackend/appsettings.json) (Backend)
- `[MODIFY]` [AiService.cs](file:///d:/Desktop/NexivraChat/backend/NexivraChatBackend/Services/AiService.cs) (Backend)

---

## 📋 Task Breakdown

### Phase 1: Environment & Database Configuration (P0)
*Thực hiện bởi: `devops-engineer` | Kỹ năng: `bash-linux`, `powershell-windows`*

#### Task 1.1: Tạo file Docker Compose khởi chạy PostgreSQL
- **Mô tả:** Tạo file `docker-compose.yml` ở thư mục gốc để định nghĩa dịch vụ PostgreSQL khớp với Connection String hiện tại.
- **INPUT:** Cấu hình Connection String từ `appsettings.json`.
- **OUTPUT:** File [docker-compose.yml](file:///d:/Desktop/NexivraChat/docker-compose.yml).
- **VERIFY:** Chạy lệnh `docker compose up -d` và kiểm tra container hoạt động, kết nối được DB qua cổng 5432.

#### Task 1.2: Cập nhật appsettings.json của Backend
- **Mô tả:** Thêm khóa cấu hình rỗng `"Gemini": { "ApiKey": "" }` vào `appsettings.json` để làm tài liệu hướng dẫn và làm cấu hình mặc định.
- **INPUT:** [appsettings.json](file:///d:/Desktop/NexivraChat/backend/NexivraChatBackend/appsettings.json).
- **OUTPUT:** Tệp JSON chứa cấu hình Gemini.
- **VERIFY:** Đọc file thấy có trường `Gemini:ApiKey`.

---

### Phase 2: Core Backend - Fallback logic & Build Check (P1)
*Thực hiện bởi: `backend-specialist` | Kỹ năng: `api-patterns`*

#### Task 2.1: Cập nhật AiService.cs hỗ trợ Biến môi trường
- **Mô tả:** Cho phép nạp API Key từ cả `Configuration` và biến môi trường hệ thống `GEMINI_API_KEY`.
- **INPUT:** [AiService.cs](file:///d:/Desktop/NexivraChat/backend/NexivraChatBackend/Services/AiService.cs).
- **OUTPUT:** Mã nguồn được cập nhật.
- **VERIFY:** Build Backend bằng `dotnet build` thành công.

---

### Phase 3: Frontend - Fix compile errors & Install Dependencies (P2)
*Thực hiện bởi: `frontend-specialist` | Kỹ năng: `frontend-design`*

#### Task 3.1: Sửa lỗi import icon ở CopilotPanel.tsx
- **Mô tả:** Đổi `<LightBulbOutlined />` ở dòng 94 thành `<BulbOutlined />` để khớp với danh sách import.
- **INPUT:** [CopilotPanel.tsx](file:///d:/Desktop/NexivraChat/frontend/nexivra-chat-frontend/src/components/CopilotPanel.tsx).
- **OUTPUT:** Mã nguồn sạch không còn tham chiếu sai.
- **VERIFY:** Trình biên dịch không báo lỗi thiếu biến hoặc kiểu.

#### Task 3.2: Cài đặt thư viện và Build kiểm thử Frontend
- **Mô tả:** Chạy `npm install` tại thư mục frontend và thực hiện build kiểm thử.
- **INPUT:** Thư mục `frontend/nexivra-chat-frontend`.
- **OUTPUT:** Thư mục `node_modules` và thư mục `dist` khi build.
- **VERIFY:** Chạy lệnh `npm run build` hoàn thành không có lỗi.

---

## 🏁 Phase X: Final Verification Plan

Bạn cần thực hiện đầy đủ các bước kiểm tra chất lượng dưới đây:

### Automated Verification
- [ ] Chạy lệnh `dotnet build` tại backend thành công.
- [ ] Chạy lệnh `npm run build` tại frontend thành công.
- [ ] Chạy kiểm tra bảo mật: `python .agents/skills/vulnerability-scanner/scripts/security_scan.py .`

### Manual Verification
- [ ] Chạy `docker compose up -d` khởi chạy PostgreSQL.
- [ ] Khởi chạy Backend (`dotnet run` trên port 5182).
- [ ] Khởi chạy Frontend (`npm run dev` trên port 5173).
- [ ] Mở trình duyệt ở chế độ ẩn danh (để có 2 phiên làm việc), kết nối và gửi tin nhắn thời gian thực qua lại.
- [ ] Thử nghiệm lệnh `@copilot` ở cả 2 trường hợp:
  - Khi không có API Key (xác nhận xem tin nhắn giả lập có chạy chữ không).
  - Khi thiết lập khóa `GEMINI_API_KEY` (xác nhận xem câu trả lời thực tế từ AI có hiển thị không).
- [ ] Kiểm tra giao diện xem có tuân thủ Purple Ban (không sử dụng màu tím) hay không.
