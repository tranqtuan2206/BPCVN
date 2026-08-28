using BPCVN.Data;
using BPCVN.Services;
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
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)
    )
);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Cấu hình Rate Limiting (giới hạn số lần thử đăng nhập)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginRateLimit", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(15);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        // Chuyển hướng tới trang Login với cờ locked=true để hiện thông báo
        context.HttpContext.Response.Redirect("/Auth/Login?locked=true");
        await Task.CompletedTask;
    };
});

// Đăng ký dịch vụ gửi email (xác thực tài khoản)
builder.Services.AddScoped<EmailService>();

// Đăng ký dịch vụ xử lý file âm thanh / tách âm từ video
builder.Services.AddScoped<AudioService>();

// Đăng ký dịch vụ upload ảnh lên Cloudinary
builder.Services.AddScoped<ImageService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
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

    // Khởi tạo DbSeeder với IConfiguration (không còn hardcode mật khẩu)
    var seeder = new DbSeeder(config);
    await seeder.SeedAsync(db);
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

    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
