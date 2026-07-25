using System.Security.Claims;
using BPCVN.Data;
using BPCVN.Models.Entities;
using BPCVN.Models.ViewModels;
using BPCVN.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BPCVN.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext    _db;
    private readonly IEmailService   _emailService;
    private readonly IMemoryCache    _cache;

    // Cấu hình rate-limit cho login
    private const int    MaxLoginAttempts   = 5;                         // Số lần thử tối đa
    private const int    LockoutMinutes     = 15;                        // Thời gian khóa (phút)
    private const string LoginAttemptPrefix = "login_attempt:";          // Prefix cache key

    // Inject IMemoryCache để track số lần login sai theo email
    public AuthController(AppDbContext db, IEmailService emailService, IMemoryCache cache)
    {
        _db           = db;
        _emailService = emailService;
        _cache        = cache;
    }

    // ── REGISTER ──────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var email    = vm.Email.Trim().ToLower();
        var username = vm.Username.Trim();

        // ── Enumeration-safe: không tiết lộ email có tồn tại không ───────────
        // Username báo lỗi bình thường (không nhạy cảm bằng email)
        // Email: nếu tồn tại → bỏ qua silently; nếu chưa → tạo user và gửi mail
        var emailExists    = await _db.Users.AnyAsync(u => u.Email    == email);
        var usernameExists = await _db.Users.AnyAsync(u => u.Username == username);

        if (usernameExists)
        {
            ModelState.AddModelError(nameof(vm.Username), "Username này đã được sử dụng.");
            return View(vm);
        }

        if (!emailExists)
        {
            // Chỉ tạo user mới khi email chưa tồn tại
            var verificationToken = Guid.NewGuid().ToString();

            var user = new User
            {
                Username          = username,
                Email             = email,
                PasswordHash      = BCrypt.Net.BCrypt.HashPassword(vm.Password),
                Role              = "User",
                CreatedAt         = DateTime.UtcNow,
                IsEmailConfirmed  = false,
                VerificationToken = verificationToken
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // ── Gửi email xác thực ──────────────────────────────────────────
            var verifyLink = Url.Action(
                action:     "VerifyEmail",
                controller: "Auth",
                values:     new { email = user.Email, token = verificationToken },
                protocol:   Request.Scheme
            );

            var emailBody = $@"
                <h2>Chào mừng bạn đến với BPCVN!</h2>
                <p>Xin chào <strong>{user.Username}</strong>,</p>
                <p>Vui lòng nhấn vào link bên dưới để kích hoạt tài khoản của bạn:</p>
                <p><a href='{verifyLink}' style='display:inline-block;padding:10px 20px;background:#333;color:#fff;text-decoration:none;border-radius:5px;'>
                    ✅ Kích hoạt tài khoản
                </a></p>
                <p>Hoặc copy link này vào trình duyệt:</p>
                <p>{verifyLink}</p>
                <hr/>
                <p style='color:#888;font-size:12px;'>Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.</p>
            ";

            try
            {
                await _emailService.SendEmailAsync(user.Email, "BPCVN - Xác thực tài khoản", emailBody);
            }
            catch (Exception)
            {
                // Nếu gửi mail lỗi, vẫn tạo tài khoản thành công
            }
        }
        // Nếu email đã tồn tại: không làm gì thêm → message chung bên dưới

        // Luôn hiển thị message chung — không tiết lộ email có tồn tại không
        TempData["Success"] = "toast.auth.register.success";
        return RedirectToAction("Login");
    }



    // ── VERIFY EMAIL ─────────────────────────────────────────────────────────

    /// <summary>
    /// Action xác thực email — user click link trong email sẽ gọi tới đây.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> VerifyEmail(string email, string token)
    {
        // Validate tham số đầu vào
        if (string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "toast.auth.verify.invalid";
            return RedirectToAction("Login");
        }

        // ── Enumeration-safe: chỉ tìm theo token (unique) ───────────────────────────────
        // Không cần check email trong WHERE clause → tránh timing attack lộ email tồn tại
        var user = await _db.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);

        if (user == null)
        {
            TempData["Error"] = "toast.auth.verify.expired";
            return RedirectToAction("Login");
        }

        // Đã tìm thấy → kích hoạt tài khoản
        user.IsEmailConfirmed = true;
        user.VerificationToken = null; // Xóa token sau khi xác thực
        await _db.SaveChangesAsync();

        TempData["Success"] = "toast.auth.verify.success";
        return RedirectToAction("Login");
    }

    // ── LOGIN ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // Nếu đã đăng nhập rồi thì redirect về Home
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);

        // ── Rate-limit: kiểm tra xem email có đang bị tạm khóa không ─────────
        var email      = vm.Email.Trim().ToLower();
        var cacheKey   = LoginAttemptPrefix + email;
        var attempts   = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(LockoutMinutes);
            return 0;
        });

        if (attempts >= MaxLoginAttempts)
        {
            TempData["Error"] = "toast.auth.login.locked";
            return RedirectToAction("Login");
        }

        var user = await _db.Users
                            .FirstOrDefaultAsync(u => u.Email == email);

        // Kiểm tra user tồn tại và password đúng
        if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
            // Tăng counter thất bại, giữ nguyên thời gian expire hiện tại
            _cache.Set(cacheKey, attempts + 1, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(LockoutMinutes)
            });

            // Thêm delay nhỏ để chống timing attack
            await Task.Delay(TimeSpan.FromMilliseconds(300));

            TempData["Error"] = "toast.auth.login.invalid";
            return RedirectToAction("Login");
        }

        if (!user.IsEmailConfirmed && user.Role != "Admin")
        {
            TempData["Error"] = "toast.auth.login.unverified";
            return RedirectToAction("Login");
        }

        // ── Login thành công → xóa counter để reset lockout ─────────────────
        _cache.Remove(cacheKey);

        await SignInUser(user, isPersistent: vm.RememberMe);

        TempData["Success"] = "toast.auth.login.success";
        TempData["SuccessParam"] = user.Username;

        // Redirect về returnUrl nếu hợp lệ, không thì về Home
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    // ── LOGOUT ────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "toast.auth.logout.success";
        return RedirectToAction("Index", "Home");
    }

    // ── CHANGE PASSWORD ───────────────────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Lấy user hiện tại từ cookie claims
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        // Xác minh mật khẩu hiện tại
        if (!BCrypt.Net.BCrypt.Verify(vm.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Mật khẩu hiện tại không đúng.");
            return View(vm);
        }

        // Cập nhật mật khẩu mới (đã hash)
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.NewPassword);
        await _db.SaveChangesAsync();

        // Đăng nhập lại để refresh cookie claims
        await SignInUser(user, isPersistent: false);

        TempData["Success"] = "toast.auth.changePassword.success";
        return RedirectToAction("Profile", "User");
    }

    // ── ACCESS DENIED ─────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ── HELPER ───────────────────────────────────────────────────────────────

    private async Task SignInUser(User user, bool isPersistent)
    {
        // Tạo Claims — thông tin được lưu trong cookie
        // Bao gồm Role claim để [Authorize(Roles = "Admin")] hoạt động
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name,           user.Username),
            new(ClaimTypes.Email,          user.Email),
            new(ClaimTypes.Role,           user.Role)  // ← Quan trọng: Role claim
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = isPersistent });
    }
}