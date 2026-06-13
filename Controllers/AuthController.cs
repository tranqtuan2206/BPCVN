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

namespace BPCVN.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    // Inject thêm IEmailService để gửi mail xác thực
    public AuthController(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    // ── REGISTER ──────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Kiểm tra email đã tồn tại chưa
        if (await _db.Users.AnyAsync(u => u.Email == vm.Email))
        {
            ModelState.AddModelError(nameof(vm.Email), "Email này đã được sử dụng.");
            return View(vm);
        }

        // Kiểm tra username đã tồn tại chưa
        if (await _db.Users.AnyAsync(u => u.Username == vm.Username))
        {
            ModelState.AddModelError(nameof(vm.Username), "Username này đã được sử dụng.");
            return View(vm);
        }

        // Tạo token xác thực email (Guid ngẫu nhiên)
        var verificationToken = Guid.NewGuid().ToString();

        // Tạo user mới — chưa kích hoạt email (IsEmailConfirmed = false)
        var user = new User
        {
            Username = vm.Username.Trim(),
            Email = vm.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            IsEmailConfirmed = false,         // Chưa xác thực
            VerificationToken = verificationToken // Token để verify
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // ── Gửi email xác thực ──────────────────────────────────────────────
        // Sinh link kích hoạt trỏ tới action VerifyEmail
        var verifyLink = Url.Action(
            action: "VerifyEmail",
            controller: "Auth",
            values: new { email = user.Email, token = verificationToken },
            protocol: Request.Scheme // https hoặc http tùy môi trường
        );

        // Nội dung email HTML
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
            // User có thể yêu cầu gửi lại sau
        }

        // KHÔNG tự động đăng nhập — yêu cầu xác thực email trước
        TempData["Success"] = "Đăng ký thành công! Vui lòng kiểm tra email để kích hoạt tài khoản trước khi đăng nhập.";
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
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Link xác thực không hợp lệ.";
            return RedirectToAction("Login");
        }

        // Tìm user theo email và token
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email == email.Trim().ToLower() && u.VerificationToken == token);

        if (user == null)
        {
            TempData["Error"] = "Link xác thực không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction("Login");
        }

        // Đã tìm thấy → kích hoạt tài khoản
        user.IsEmailConfirmed = true;
        user.VerificationToken = null; // Xóa token sau khi xác thực
        await _db.SaveChangesAsync();

        TempData["Success"] = "Xác thực thành công! Bạn có thể đăng nhập ngay bây giờ.";
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

        var user = await _db.Users
                            .FirstOrDefaultAsync(u => u.Email == vm.Email.Trim().ToLower());

        // Kiểm tra user tồn tại và password đúng
        if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(vm);
        }

        // ── Kiểm tra xác thực email ─────────────────────────────────────────
        // Bỏ qua kiểm tra cho tài khoản có Role = "Admin"
        if (!user.IsEmailConfirmed && user.Role != "Admin")
        {
            ModelState.AddModelError(string.Empty,
                "Tài khoản chưa được kích hoạt, vui lòng kiểm tra email.");
            return View(vm);
        }

        await SignInUser(user, isPersistent: vm.RememberMe);

        TempData["Success"] = $"Đăng nhập thành công. Xin chào {user.Username}!";

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
        TempData["Success"] = "Đã đăng xuất thành công.";
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

        TempData["Success"] = "Đổi mật khẩu thành công!";
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