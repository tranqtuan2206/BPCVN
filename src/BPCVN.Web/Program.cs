using System.Net;
using System.Threading.RateLimiting;
using BPCVN.Application.Interfaces;
using BPCVN.Application.Services;
using BPCVN.Infrastructure.Data;
using BPCVN.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

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

// Cấu hình Forwarded Headers để nhận đúng HTTPS và IP gốc khi chạy sau Cloudflare Proxy / IIS
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Chấp nhận header chuyển tiếp từ reverse proxy bên ngoài (Cloudflare)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
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
// Phải đặt UseForwardedHeaders trước HttpsRedirection, Canonical Redirect và RateLimiter
app.UseForwardedHeaders();

// Tự động chuyển hướng tên miền tạm của hosting (*.etempurl.com) sang domain chính thức bpcvn.dev
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var redirectUrl = Program.GetCanonicalRedirectUrl(
            context, app.Environment.IsDevelopment(), app.Configuration);
        if (redirectUrl != null)
        {
            context.Response.Redirect(redirectUrl, permanent: false);
            return;
        }
        await next();
    });

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
    public const string DefaultProductionBaseUrl = "https://bpcvn.dev";

    /// <summary>
    /// Xác định URL gốc công khai dùng để tạo link trong email.
    /// Ưu tiên AppSettings:PublicBaseUrl; ở Production tự fallback về https://bpcvn.dev.
    /// </summary>
    public static string? ResolvePublicBaseUrl(IConfiguration config, bool isDevelopment)
    {
        var configured = config["AppSettings:PublicBaseUrl"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured.TrimEnd('/');

        return isDevelopment ? null : DefaultProductionBaseUrl;
    }

    /// <summary>
    /// Tạo URL chuyển hướng về domain chính nếu người dùng truy cập trực tiếp tên miền tạm của hosting (*.etempurl.com).
    /// Bỏ qua nếu request đi qua Cloudflare Proxy (có header CF-Ray) để tránh vòng lặp redirect.
    /// </summary>
    public static string? GetCanonicalRedirectUrl(HttpContext context, bool isDevelopment, IConfiguration config)
    {
        if (isDevelopment)
            return null;

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            return null;

        // Tránh vòng lặp nếu request đã đi qua Cloudflare Proxy nhưng origin host bị rewrite về .etempurl.com
        if (context.Request.Headers.ContainsKey("CF-Ray"))
            return null;

        var host = context.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host) ||
            !host.EndsWith(".etempurl.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var baseUrl = ResolvePublicBaseUrl(config, isDevelopment);
        if (string.IsNullOrEmpty(baseUrl))
            return null;

        return $"{baseUrl}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
    }

    /// <summary>
    /// Lấy địa chỉ IP thực của client (ưu tiên header CF-Connecting-IP khi bật Cloudflare Proxy).
    /// </summary>
    public static string GetClientIpKey(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfIpValues))
        {
            var rawCfIp = cfIpValues.ToString().Split(',', StringSplitOptions.TrimEntries)[0];
            if (IPAddress.TryParse(rawCfIp, out var parsedIp))
                return parsedIp.ToString();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Tạo phân vùng giới hạn tần suất theo địa chỉ IP thực của client.
    /// </summary>
    public static RateLimitPartition<string> CreateIpFixedWindowPartition(
        HttpContext httpContext, int permitLimit, int windowMinutes)
    {
        var ipKey = GetClientIpKey(httpContext);
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
