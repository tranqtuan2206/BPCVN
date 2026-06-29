using BPCVN.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // Include đầy đủ quan hệ trong 1 query — tránh N+1
        var specs = await _db.Specs
            .IgnoreQueryFilters()
            .Include(s => s.User)
            .Include(s => s.Kit)
            .Include(s => s.Switch)
            .Include(s => s.SoundTests)
                .ThenInclude(st => st.Likes)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(15)                           // Phase 3: tăng lên 15
            .ToListAsync();

        ViewBag.KitCount = await _db.Kits.CountAsync();
        ViewBag.SwitchCount = await _db.Switches.CountAsync();
        ViewBag.SpecCount = await _db.Specs.CountAsync();

        return View(specs);
    }

    // ── Action xử lý trang lỗi chung (Exception Handler) ────────────────
    // Khớp với cấu hình app.UseExceptionHandler("/Home/Error") trong Program.cs
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}