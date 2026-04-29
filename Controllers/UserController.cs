using System.Security.Claims;
using BPCVN.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

[Authorize]
public class UserController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public UserController(AppDbContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    // ── PROFILE ───────────────────────────────────────────────────────────────
    // GET /User/Profile

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return RedirectToAction("Login", "Auth");

        // ThenInclude để load nested — tránh N+1 query
        var user = await _db.Users
            .Include(u => u.Specs)
                .ThenInclude(s => s.Kit)
            .Include(u => u.Specs)
                .ThenInclude(s => s.Switch)
            .Include(u => u.Specs)
                .ThenInclude(s => s.SoundTests)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null) return NotFound();

        return View(user);
    }

    // ── DELETE SPEC ───────────────────────────────────────────────────────────
    // POST /User/DeleteSpec — Giữ lại cho backward compatibility với Profile view

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSpec(Guid specId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        // Chỉ lấy spec thuộc về user hiện tại — bảo vệ tránh xóa của người khác
        // Admin cũng có thể xóa bất kỳ build nào
        var isAdmin = User.IsInRole("Admin");
        var spec = await _db.Specs
            .Include(s => s.SoundTests)
            .FirstOrDefaultAsync(s => s.SpecId == specId &&
                (s.UserId == userId || isAdmin));

        if (spec == null) return NotFound();

        // Xóa file âm thanh vật lý khỏi wwwroot
        foreach (var st in spec.SoundTests)
        {
            var relativePath = st.AudioUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(_env.WebRootPath, relativePath);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        // EF Cascade tự xóa SoundTests khi xóa Spec
        _db.Specs.Remove(spec);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã xóa build \"{spec.BuildName}\" thành công.";
        return RedirectToAction(nameof(Profile));
    }
}