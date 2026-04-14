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
        // Lấy 10 Spec mới nhất, Include tất cả liên quan — 1 query duy nhất
        var specs = await _db.Specs
            .Include(s => s.User)
            .Include(s => s.Kit)
            .Include(s => s.Switch)
            .Include(s => s.SoundTests)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Stats cho hero section
        ViewBag.KitCount    = await _db.Kits.CountAsync();
        ViewBag.SwitchCount = await _db.Switches.CountAsync();
        ViewBag.SpecCount   = await _db.Specs.CountAsync();

        return View(specs);
    }
}