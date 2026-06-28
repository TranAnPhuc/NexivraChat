# Spec: Deploy NexivraChat (free + auto-deploy, người ở xa truy cập được)

> Qua `/plan-ceo-review` (2026-06-28). Mode: **SELECTIVE EXPANSION**.
> Mô hình: Claude lập plan — **Antigravity 2.0 code**.
> Kiến trúc chốt: **Render (backend .NET Docker) + Cloudflare Pages (frontend tĩnh) + Neon (Postgres) + Gemini free**.

## 0. Vì sao kiến trúc này

`.NET 8 + SignalR` giữ **WebSocket bền vững** → KHÔNG hợp serverless (Vercel/Netlify/Cloudflare Functions). Cần host container luôn-chạy-được cho backend.

| Lớp | Nơi đặt | Free? | Auto-deploy |
|-----|---------|-------|-------------|
| Frontend (React/Vite tĩnh) | Cloudflare Pages (hoặc Vercel) | ✔ không thẻ | ✔ git push |
| Backend (.NET 8 + SignalR, Docker) | Render — Web Service free | ✔ không thẻ, 750h/tháng | ✔ git push |
| Database (Postgres) | Neon — serverless free | ✔ không thẻ | — (`DbInitializer` tự dựng schema) |
| AI | Gemini free tier | ✔ (đang dùng) | — |

**Gotcha đã biết:** Render free ngủ sau 15' idle → request đầu tiên chờ ~50s (cold start). Chỉ chạm lớp realtime. Đã có add-on cron keep-warm (mục 4.1) xử lý.

---

## 1. BASELINE — phải có (mọi phương án đều cần)

### 1.1 Frontend: bỏ hardcode API URL
`frontend/nexivra-chat-frontend/src/services/api.ts:3`
```ts
// CŨ:
const API_BASE_URL = 'http://localhost:5182/api';
// MỚI:
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5182/api';
```
- Hub URL (`ChatView.tsx:447`) đã ăn theo `API_BASE_URL` → không cần sửa thêm.
- Thêm `frontend/nexivra-chat-frontend/.env.example`:
  ```
  VITE_API_BASE_URL=https://<ten-backend>.onrender.com/api
  ```
- Đảm bảo `.env*` (trừ `.env.example`) nằm trong `.gitignore`.

### 1.2 Backend: CORS đọc từ env
`backend/NexivraChatBackend/Program.cs:73-78`
```csharp
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"]
        ?? "http://localhost:5173,http://127.0.0.1:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()                 // BẮT BUỘC cho SignalR
              .WithExposedHeaders("X-Partner-Last-Read-Id")); // giữ header GĐ4.5
});
```
- Env var prod: `Cors__AllowedOrigins=https://<ten-fe>.pages.dev` (chú ý `__` = nested key trong .NET).
- **Không** dùng `AllowAnyOrigin()` cùng `AllowCredentials()` — SignalR sẽ vỡ.

### 1.3 Backend: secrets đọc từ env (không hardcode)
- Connection string: `ConnectionStrings__DefaultConnection` (Neon).
- JWT secret: `Jwt__Key` (hoặc đúng key hiện có trong `appsettings.json`).
- Gemini: `Gemini__ApiKey` (hoặc key đang đọc trong `AiService`/`TranslationService`).
- `appsettings.json` giữ placeholder rỗng; **không commit giá trị thật**. .NET tự override config bằng env var.

### 1.4 Backend: Dockerfile (mới, ở `backend/NexivraChatBackend/Dockerfile`)
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY NexivraChatBackend.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
# Render cấp cổng qua $PORT; ASP.NET đọc ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NexivraChatBackend.dll"]
```
- Trên Render set env `ASPNETCORE_URLS=http://0.0.0.0:10000` nếu Render cấp `$PORT=10000`, hoặc dùng `PORT` → đặt `ASPNETCORE_URLS=http://0.0.0.0:$PORT`. (Render mặc định expose cổng app lắng nghe; khớp `EXPOSE`/`ASPNETCORE_URLS`.)
- Nếu csproj không nằm ngay `backend/NexivraChatBackend/`, chỉnh path COPY cho đúng.

### 1.5 Neon Postgres
1. Tạo project Neon free → lấy connection string (dạng `postgresql://user:pass@host/db?sslmode=require`).
2. **Quan trọng:** Npgsql cần `Ssl Mode=Require;Trust Server Certificate=true` (hoặc giữ `sslmode=require` trong URL). Test `DapperContext` kết nối được TLS.
3. Không cần migration tay — `DbInitializer` chạy idempotent lúc khởi động, tự tạo bảng + index + seed phòng mặc định.

### 1.6 Render (backend) — dashboard, không code
1. New → Web Service → connect GitHub repo, branch deploy (`main` hoặc branch chọn).
2. Runtime: **Docker**, Dockerfile path: `backend/NexivraChatBackend/Dockerfile`, root context phù hợp.
3. Env vars: `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Gemini__ApiKey`, `Cors__AllowedOrigins`, `ASPNETCORE_URLS`.
4. Auto-Deploy: **On** (push là build lại).
5. Lấy URL: `https://<ten-backend>.onrender.com`.

### 1.7 Cloudflare Pages (frontend) — dashboard, không code
1. Pages → connect repo → root: `frontend/nexivra-chat-frontend`.
2. Build command `npm run build`, output `dist`.
3. Env var build: `VITE_API_BASE_URL=https://<ten-backend>.onrender.com/api`.
4. Deploy → lấy URL `https://<ten-fe>.pages.dev` → **set ngược lại** vào `Cors__AllowedOrigins` của Render (mục 1.2) rồi redeploy backend.

### 1.8 Thứ tự bật (tránh deadlock cấu hình)
Neon → Render (backend, có URL) → Cloudflare (FE trỏ về backend) → cập nhật CORS Render = URL FE → redeploy backend → test.

---

## 2. ADD-ON đã chốt (cherry-pick: cả 4)

### 4.1 Cron keep-warm chống cold-start  [Effort S]
- Endpoint nhẹ `/health` (mục 4.2) làm đích ping.
- Dùng **cron-job.org** (free) hoặc **GitHub Actions** schedule mỗi 10':
  ```yaml
  # .github/workflows/keepwarm.yml
  name: keep-warm
  on:
    schedule: [{ cron: '*/10 * * * *' }]
    workflow_dispatch:
  jobs:
    ping:
      runs-on: ubuntu-latest
      steps:
        - run: curl -fsS https://<ten-backend>.onrender.com/health || true
  ```
- Lưu ý: ping liên tục ăn vào 750h/tháng của Render. 1 instance ping 24/7 vẫn < 750h → ổn. Nếu muốn tiết kiệm, chỉ ping giờ ban ngày.

### 4.2 Health check `/health`  [Effort S]
`Program.cs`:
```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);
// ...
app.MapHealthChecks("/health");
```
- Trả 200 khi DB sống. Render dùng làm health check path; cron keep-warm ping vào đây (rẻ, không đụng API thật).

### 4.3 Secret hardening  [Effort S] — gồm vá lỗ bảo mật đã phát hiện
- ⚠️ `docker-compose.yml:10` commit mật khẩu DB thật `Boggyroom24032005@` vào git. **Đổi mật khẩu này ở mọi nơi bạn còn dùng nó** (nó trông như mật khẩu cá nhân). Chuyển compose sang đọc env:
  ```yaml
  environment:
    POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-changeme}
  ```
- Sinh JWT secret prod mạnh (≥32 byte random, khác placeholder dev), đặt qua `Jwt__Key` env trên Render.
- Rà `appsettings.json` không còn giá trị thật nào bị commit.

### 4.4 Observability (log có cấu trúc)  [Effort S-M]
- Thêm **Serilog** (`Serilog.AspNetCore` + sink Console JSON) → log ra stdout, Render capture free.
- Log tối thiểu: SignalR `OnConnectedAsync`/`OnDisconnectedAsync` (kèm userId), exception trong hub, lỗi gọi Gemini.
- Mục tiêu: khi bạn bè ở xa báo "chat không vào được", có log để soi (CORS reject? token? DB?).

---

## 3. KHÔNG nằm trong scope (đẩy TODOS.md)
- CI/CD chạy `dotnet test` (Testcontainers) trước khi deploy — to, để sau khi muốn production thật.
- Custom domain, staging env, monitoring/alert chủ động, auto-rollback.
- Redis backplane cho SignalR scale nhiều instance (giờ 1 instance free là đủ; trần scale đã biết).

## 4. Rủi ro & edge case cần test sau deploy
1. **SignalR qua WSS:** trang HTTPS phải mở `wss://` tới backend HTTPS — Render + Cloudflare đều cấp TLS, nhưng test reconnect khi đổi mạng.
2. **Cold-start UX:** trước khi bật keep-warm, vào sau 15' idle → SignalR treo ~50s. Cần loading/"đang kết nối…" state ở `ChatView` (kiểm tra đã có chưa).
3. **CORS + credentials:** sai 1 ký tự origin → SignalR negotiate 401/CORS fail. Test bằng FE thật, không phải localhost.
4. **Neon autosuspend:** DB ngủ khi idle → query đầu chậm vài trăm ms. Chấp nhận được.
5. **Gemini free rate-limit:** nhiều người chat AI cùng lúc có thể chạm giới hạn → `AiService` đã có fallback mock mode, xác nhận nó kích hoạt đúng.

## 5. Bàn giao Antigravity — thứ tự code
1. Mục 1.1–1.4 (sửa hardcode + Dockerfile) — code thuần, test build local.
2. Mục 4.2 `/health` + 4.3 secret hardening + 4.4 Serilog — cùng đợt backend.
3. Mục 1.5–1.8 — dashboard setup (bạn làm, không phải Antigravity).
4. Mục 4.1 keep-warm — sau khi có URL backend.
