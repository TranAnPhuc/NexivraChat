using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NexivraChatBackend.Data;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;
using NexivraChatBackend.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký các dịch vụ cốt lõi
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<NexivraChatBackend.Services.PresenceTracker>();

// 2. Đăng ký DapperContext và các Repositories
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<RoomRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<PrivateChatRepository>();
builder.Services.AddScoped<ProfileRepository>();

// 3. Đăng ký các Services phụ trợ
builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpClient<AiService>();
builder.Services.AddHttpClient<TranslationService>();

// 4. Cấu hình JWT Authentication
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "DefaultSuperSecretKey1234567890123456";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "NexivraChat";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "NexivraChat";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Hỗ trợ truyền Token qua Query String khi kết nối SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// 5. Cấu hình CORS để frontend React kết nối
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Rất quan trọng cho SignalR!
    });
});

var app = builder.Build();

// 6. Khởi tạo Database
try
{
    var context = app.Services.GetRequiredService<DapperContext>();
    DbInitializer.Initialize(context);
    Console.WriteLine("Database initialized successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error initializing database: {ex.Message}");
}

// 6b. Đảm bảo thư mục lưu avatar tồn tại để UseStaticFiles phục vụ được.
var avatarsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "avatars");
Directory.CreateDirectory(avatarsPath);

// 7. Cấu hình HTTP Request Pipeline
app.UseCors("CorsPolicy");

app.UseHttpsRedirection();

// Phục vụ file tĩnh (avatar) từ wwwroot.
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

app.Run();