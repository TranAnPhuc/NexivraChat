using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NexivraChatBackend.Data;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;
using NexivraChatBackend.Hubs;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 0. Cấu hình Serilog log có cấu trúc (Console JSON ra stdout)
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

// 1. Đăng ký các dịch vụ cốt lõi
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<NexivraChatBackend.Services.PresenceTracker>();

// 2. Đăng ký Health Check (mục 4.2)
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

// 3. Đăng ký DapperContext và các Repositories
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<RoomRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<PrivateChatRepository>();
builder.Services.AddScoped<ProfileRepository>();
builder.Services.AddScoped<ConversationReadRepository>();
builder.Services.AddScoped<ReactionRepository>();
builder.Services.AddScoped<MentionRepository>();

// 4. Đăng ký các Services phụ trợ
builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpClient<AiService>();
builder.Services.AddHttpClient<TranslationService>();

// 5. Cấu hình JWT Authentication (đọc từ JwtSettings:Secret hoặc Jwt:Key hoặc env Jwt__Key)
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? builder.Configuration["Jwt:Key"] ?? "DefaultSuperSecretKey1234567890123456";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? builder.Configuration["Jwt:Issuer"] ?? "NexivraChat";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? builder.Configuration["Jwt:Audience"] ?? "NexivraChat";

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

    // Hỗ trợ truyền Token qua Query String khi kết nối SignalR và tải file đính kèm (/api/files)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/api/files")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// 6. Cấu hình CORS để đọc từ env config (Cors:AllowedOrigins)
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

var app = builder.Build();

// 7. Khởi tạo Database
try
{
    var context = app.Services.GetRequiredService<DapperContext>();
    DbInitializer.Initialize(context);
    Log.Information("Database initialized successfully.");
}
catch (Exception ex)
{
    Log.Error(ex, "Error initializing database");
}

// 8. Cấu hình HTTP Request Pipeline
app.UseCors("CorsPolicy");

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        var contentType = ctx.Context.Response.ContentType ?? "";
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.Append("Content-Disposition", "attachment");
        }
    }
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

app.Run();

public partial class Program { }