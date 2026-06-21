# Project: AI Chat Realtime MVP

## Tech Stack Cố Định

- Backend: ASP.NET Core Web API, SignalR Hub
- Database: PostgreSQL + Dapper (Sử dụng DapperContext.cs để quản lý connection)
- Frontend: React TypeScript + Ant Design (antd)

## Cấu trúc thư mục hiện tại

- `Backend/Data/DapperContext.cs` (Đã tạo - quản lý kết nối DB)
- `Backend/Models/` (Đã có các Entity: User, ChatRoom, Message)

## Quy tắc Code

- Luôn sử dụng Dapper để viết SQL thuần, không dùng Entity Framework.
- Tên bảng và thuộc tính trong SQL phải khớp chính xác với file Entity trong thư mục Models.

## Tiến trình cập nhật hàm

- Hàm `GetOldMessages(int limit, int offset)` đã được cập nhật trong class `MessageRepository.cs`.
- Hàm `SaveNewMessage(Message message)` đã được cập nhật trong class `MessageRepository.cs`.
