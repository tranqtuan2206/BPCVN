using System.Security.Claims;
using BPCVN.Data;
using BPCVN.Models.Entities;
using BPCVN.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db) => _db = db;

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

        // Tạo user mới — Role mặc định "User" (từ model default)
        var user = new User
        {
            Username = vm.Username.Trim(),
            Email = vm.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
            Role = "User", // Luôn gán Role User cho đăng ký thường
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Tự động đăng nhập sau khi đăng ký
        await SignInUser(user, isPersistent: false);

        TempData["Success"] = $"Chào mừng {user.Username}! Tài khoản đã được tạo.";
        return RedirectToAction("Index", "Home");
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