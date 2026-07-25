using BPCVN.Data;
using BPCVN.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
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

// Đăng ký IMemoryCache (dùng cho rate-limit login)
builder.Services.AddMemoryCache();

// Đăng ký dịch vụ gửi email (xác thực tài khoản)
builder.Services.AddScoped<IEmailService, EmailService>();

// Đăng ký dịch vụ xử lý file âm thanh / tách âm từ video
builder.Services.AddScoped<IAudioService, AudioService>();

// Đăng ký dịch vụ upload ảnh lên Cloudinary
builder.Services.AddScoped<IImageService, ImageService>();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
