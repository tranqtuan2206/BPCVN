using System.Security.Claims;
using BPCVN.Application.Services;
using BPCVN.Application.ViewModels;
using BPCVN.Domain.Entities;
using BPCVN.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace BPCVN.Web.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext           _db;
    private readonly EmailService          _emailService;
    private readonly IConfiguration        _config;
    private readonly IWebHostEnvironment   _env;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        EmailService emailService,
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<AuthController> logger)
    {
        _db           = db;
        _emailService = emailService;
        _config       = config;
        _env          = env;
        _logger       = logger;
    }

    // ── REGISTER ──────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("AuthActionRateLimit")]
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

            // ── Gửi email xác thực (rút gọn URL, chỉ truyền token) ───────────
            var verifyLink = BuildPublicActionUrl(
                "VerifyEmail",
                "Auth",
                new { token = verificationToken });

            var (verifySubject, emailBody) = AuthEmailTemplateBuilder.BuildVerifyAccountEmail(
                user.Username, verifyLink, GetPreferredEmailLanguage());

            try
            {
                await _emailService.SendEmailAsync(user.Email, verifySubject, emailBody);
            }
            catch (Exception ex)
            {
                // Nếu gửi mail lỗi, vẫn tạo tài khoản thành công và ghi log
                _logger.LogWarning(ex, "[Auth] Không thể gửi email xác thực tới {UserId}", user.UserId);
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
    [EnableRateLimiting("AuthActionRateLimit")]
    public async Task<IActionResult> VerifyEmail(string? token, string? email = null)
    {
        // Validate tham số đầu vào
        if (string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "toast.auth.verify.invalid";
            return RedirectToAction("Login");
        }

        // Chỉ tìm theo token (GUID unique) — không cần thêm email vào WHERE
        // tránh timing difference lộ thông tin email có tồn tại không
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
    public IActionResult Login(string? returnUrl = null, bool locked = false)
    {
        // Nhận cờ locked từ middleware RateLimiting
        if (locked) TempData["Error"] = "toast.auth.login.locked";

        // Nếu đã đăng nhập rồi thì redirect về Home
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginRateLimit")]
    public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);

        var email      = vm.Email.Trim().ToLower();

        var user = await _db.Users
                            .FirstOrDefaultAsync(u => u.Email == email);

        // Kiểm tra user tồn tại và password đúng
        if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
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
    [EnableRateLimiting("AuthActionRateLimit")]
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
            // Thêm độ trễ nhỏ để hạn chế dò mật khẩu
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Mật khẩu hiện tại không đúng.");
            return View(vm);
        }

        // Chặn đổi sang mật khẩu trùng với mật khẩu hiện tại
        if (BCrypt.Net.BCrypt.Verify(vm.NewPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(vm.NewPassword), "Mật khẩu mới không được trùng với mật khẩu cũ.");
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

    // ── FORGOT PASSWORD (QUÊN MẬT KHẨU) ───────────────────────────────────────

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        // Dùng TempData theo mẫu Post-Redirect-Get để tránh gửi lại form khi F5
        ViewBag.EmailSent = TempData["ForgotSent"] != null;
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("AuthActionRateLimit")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var email = vm.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Chỉ phát hành token khi tài khoản tồn tại và đã xác thực email (hoặc là Admin)
        if (user != null && (user.IsEmailConfirmed || user.Role == "Admin"))
        {
            var rawToken = PasswordResetTokenHelper.GenerateToken();
            user.PasswordResetTokenHash = PasswordResetTokenHelper.HashToken(rawToken);
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            await _db.SaveChangesAsync();

            // Rút gọn URL: chỉ truyền token, không đưa email lên query string
            var resetLink = BuildPublicActionUrl(
                "ResetPassword",
                "Auth",
                new { token = rawToken });

            var (resetSubject, emailBody) = AuthEmailTemplateBuilder.BuildPasswordResetEmail(
                user.Username, resetLink, GetPreferredEmailLanguage());

            try
            {
                await _emailService.SendEmailAsync(user.Email, resetSubject, emailBody);
            }
            catch (Exception ex)
            {
                // Ghi log để vận hành kiểm tra nhưng không làm rò rỉ trạng thái email ra ngoài
                _logger.LogWarning(ex, "[Auth] Không thể gửi email đặt lại mật khẩu tới {UserId}", user.UserId);
            }
        }

        // Độ trễ nhỏ + phản hồi giống nhau mọi trường hợp để chống email enumeration
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        TempData["ForgotSent"] = true;
        TempData["Success"] = "toast.auth.forgot.sent";
        return RedirectToAction(nameof(ForgotPassword));
    }

    // ── RESET PASSWORD (ĐẶT LẠI MẬT KHẨU) ─────────────────────────────────────

    [HttpGet]
    [EnableRateLimiting("AuthActionRateLimit")]
    public async Task<IActionResult> ResetPassword(string? token, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "toast.auth.reset.invalid";
            return RedirectToAction(nameof(ForgotPassword));
        }

        // Tìm user theo mã băm token (không cần lộ email trên URL)
        var cleanToken = token.Trim();
        var tokenHash = PasswordResetTokenHelper.HashToken(cleanToken);
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash);

        // Nếu link cũ có kèm email thì kiểm tra khớp bổ sung cho chặt chẽ
        var emailMatches = string.IsNullOrWhiteSpace(email) ||
            (user != null && string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));

        if (user == null || !emailMatches ||
            !PasswordResetTokenHelper.VerifyToken(cleanToken, user.PasswordResetTokenHash, user.PasswordResetTokenExpiry))
        {
            TempData["Error"] = "toast.auth.reset.invalid";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel
        {
            Email = user.Email,
            Token = cleanToken
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("AuthActionRateLimit")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var normalizedEmail = vm.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null || !PasswordResetTokenHelper.VerifyToken(vm.Token, user.PasswordResetTokenHash, user.PasswordResetTokenExpiry))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            TempData["Error"] = "toast.auth.reset.invalid";
            return RedirectToAction(nameof(ForgotPassword));
        }

        // Chặn đặt lại mật khẩu trùng với mật khẩu cũ (giữ nguyên token để người dùng nhập lại ngay)
        if (BCrypt.Net.BCrypt.Verify(vm.NewPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(vm.NewPassword), "Mật khẩu mới không được trùng với mật khẩu cũ.");
            return View(vm);
        }

        // Cập nhật mật khẩu mới và vô hiệu hóa token ngay để đảm bảo chỉ dùng một lần
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiry = null;
        await _db.SaveChangesAsync();

        TempData["Success"] = "toast.auth.reset.success";
        return RedirectToAction(nameof(Login));
    }

    // ── ACCESS DENIED ─────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ── HELPER ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy ngôn ngữ gửi email theo lựa chọn trên web (cookie bpcvn-lang) hoặc Accept-Language.
    /// </summary>
    private string GetPreferredEmailLanguage()
    {
        return AuthEmailTemplateBuilder.ResolveEmailLanguage(
            Request.Cookies["bpcvn-lang"],
            Request.Headers.AcceptLanguage.ToString());
    }

    /// <summary>
    /// Tạo URL công khai cho liên kết xác thực/đặt lại mật khẩu trong email.
    /// Ưu tiên AppSettings:PublicBaseUrl hoặc mặc định https://bpcvn.dev ở Production.
    /// </summary>
    private string BuildPublicActionUrl(string action, string controller, object values)
    {
        var publicBaseUrl = Program.ResolvePublicBaseUrl(_config, _env.IsDevelopment());
        if (!string.IsNullOrEmpty(publicBaseUrl))
        {
            var relativePath = Url.Action(action: action, controller: controller, values: values) ?? "/";
            return $"{publicBaseUrl}{relativePath}";
        }

        return Url.Action(action: action, controller: controller, values: values, protocol: Request.Scheme) ?? "/";
    }

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