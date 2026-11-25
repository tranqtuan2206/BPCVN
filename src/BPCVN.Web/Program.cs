using BPCVN.Application.Interfaces;
using BPCVN.Application.Services;
using BPCVN.Infrastructure.Data;
using BPCVN.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Kestrel: tăng giới hạn request cho upload video lớn ──────────────────
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // 200MB
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("BPCVN.Infrastructure");
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }
    )
);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Cấu hình Rate Limiting phân vùng theo địa chỉ IP (tránh 1 IP làm khóa toàn bộ người dùng khác)
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginRateLimit", httpContext =>
        CreateIpFixedWindowPartition(httpContext, permitLimit: 5, windowMinutes: 15));

    options.AddPolicy("AuthActionRateLimit", httpContext =>
        CreateIpFixedWindowPartition(httpContext, permitLimit: 10, windowMinutes: 15));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.HttpContext.Request.Path.StartsWithSegments("/Auth/Login", StringComparison.OrdinalIgnoreCase))
        {
            // Chuyển hướng tới trang Login với cờ locked=true để hiện thông báo
            context.HttpContext.Response.Redirect("/Auth/Login?locked=true");
            return;
        }

        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Quá nhiều yêu cầu từ địa chỉ của bạn. Vui lòng thử lại sau ít phút.", token);
    };
});

// Đăng ký dịch vụ gửi email (xác thực tài khoản)
builder.Services.AddScoped<EmailService>();

// Đăng ký dịch vụ xử lý file âm thanh / tách âm từ video
builder.Services.AddScoped<AudioService>();

// Đăng ký dịch vụ upload ảnh lên Cloudinary
builder.Services.AddScoped<ImageService>();

// Đăng ký dịch vụ truy vấn và phân trang danh mục sản phẩm
builder.Services.AddScoped<IKitService, KitService>();
builder.Services.AddScoped<ISwitchService, SwitchService>();
builder.Services.AddScoped<IKeycapService, KeycapService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        // Tăng cường bảo mật Cookie xác thực
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

var app = builder.Build();

// ── Seed Database ─────────────────────────────────────────────────────────────
// Tạo scope để resolve các service cần thiết cho việc seed dữ liệu
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // Tự động chạy migration mới khi deploy (thay cho dotnet ef database update thủ công)
    await db.Database.MigrateAsync();

    // Khởi tạo DbSeeder với cờ môi trường để tránh lộ mật khẩu sinh ngẫu nhiên ra stdout ở Production
    var seeder = new DbSeeder(config);
    await seeder.SeedAsync(db, app.Environment.IsDevelopment());
}


// ── Middleware Pipeline ───────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// ── Security Headers ─────────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    // Chống MIME-type sniffing
    headers["X-Content-Type-Options"] = "nosniff";

    // Chống Clickjacking — chỉ cho phép iframe từ cùng domain
    headers["X-Frame-Options"] = "SAMEORIGIN";

    // Bật XSS filter của trình duyệt cũ (IE/Edge legacy)
    headers["X-XSS-Protection"] = "1; mode=block";

    // Kiểm soát Referrer khi chuyển trang
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Giới hạn các browser API nguy hiểm
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

    // Chính sách bảo mật nội dung (CSP) — cho phép đúng các CDN và Cloudinary đang dùng
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net data:; " +
        "img-src 'self' data: blob: https:; " +
        "media-src 'self' blob: https://res.cloudinary.com; " +
        "connect-src 'self' https://res.cloudinary.com https://cdn.jsdelivr.net; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'self';";

    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Admin}/{action=Pending}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program
{
    /// <summary>
    /// Tạo phân vùng giới hạn tần suất theo địa chỉ IP của client.
    /// </summary>
    public static RateLimitPartition<string> CreateIpFixedWindowPartition(
        HttpContext httpContext, int permitLimit, int windowMinutes)
    {
        var ipKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(windowMinutes),
                PermitLimit = permitLimit,
                QueueLimit = 0
            });
    }
}
