using System.Security.Claims;
using BPCVN.Data;
using BPCVN.Models.Entities;
using BPCVN.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class SpecController : Controller
{
    private readonly AppDbContext _db;

    public SpecController(AppDbContext db) => _db = db;

    // ── INDEX / SEARCH & FILTER ───────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string? searchString, string? switchType)
    {
        // Dùng IQueryable để LINQ tổng hợp điều kiện rồi mới gọi SQL 1 lần
        var query = _db.Specs
            .Include(s => s.User)
            .Include(s => s.Kit)
            .Include(s => s.Switch)
            .Include(s => s.SoundTests)
            .AsNoTracking()
            .AsQueryable();

        // Lọc theo tên build / kit / switch (không phân biệt hoa thường)
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            var keyword = searchString.Trim().ToLower();
            query = query.Where(s =>
                s.BuildName.ToLower().Contains(keyword) ||
                s.Kit.Name.ToLower().Contains(keyword)  ||
                s.Switch.Name.ToLower().Contains(keyword));
        }

        // Lọc theo loại switch
        if (!string.IsNullOrWhiteSpace(switchType))
            query = query.Where(s => s.Switch.Type == switchType);

        var specs = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        // Giữ lại giá trị filter trên form sau khi submit
        ViewBag.SearchString = searchString;
        ViewBag.SwitchType   = switchType;

        return View(specs);
    }

    // ── CREATE GET ────────────────────────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new SpecCreateViewModel());
    }

    // ── CREATE POST ───────────────────────────────────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SpecCreateViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        // Lấy UserId từ cookie claims
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        // Tìm hoặc tạo mới Kit theo tên
        var kitName = vm.KitName.Trim();
        var kit = await _db.Kits
            .FirstOrDefaultAsync(k => k.Name.ToLower() == kitName.ToLower());
        if (kit == null)
        {
            kit = new Kit { Name = kitName };
            _db.Kits.Add(kit);
            await _db.SaveChangesAsync();
        }

        // Tìm hoặc tạo mới Switch theo tên
        var switchName = vm.SwitchName.Trim();
        var sw = await _db.Switches
            .FirstOrDefaultAsync(s => s.Name.ToLower() == switchName.ToLower());
        if (sw == null)
        {
            sw = new Switch { Name = switchName };
            _db.Switches.Add(sw);
            await _db.SaveChangesAsync();
        }

        // Tìm hoặc tạo mới Keycap theo tên (nếu có nhập)
        int? keycapId = null;
        if (!string.IsNullOrWhiteSpace(vm.KeycapName))
        {
            var keycapName = vm.KeycapName.Trim();
            var keycap = await _db.Keycaps
                .FirstOrDefaultAsync(k => k.Name.ToLower() == keycapName.ToLower());
            if (keycap == null)
            {
                keycap = new Keycap { Name = keycapName };
                _db.Keycaps.Add(keycap);
                await _db.SaveChangesAsync();
            }
            keycapId = keycap.KeycapId;
        }

        var spec = new Spec
        {
            UserId        = userId,
            BuildName     = vm.BuildName.Trim(),
            KitId         = kit.KitId,
            SwitchId      = sw.SwitchId,
            KeycapId      = keycapId,
            PlateMaterial = vm.PlateMaterial?.Trim(),
            FoamSetup     = vm.FoamSetup?.Trim(),
            Mods          = vm.Mods?.Trim(),
            CreatedAt     = DateTime.UtcNow
        };

        _db.Specs.Add(spec);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Build \"{spec.BuildName}\" đã được tạo thành công!";
        return RedirectToAction(nameof(Details), new { id = spec.SpecId });
    }

    // ── DETAILS ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var spec = await _db.Specs
            .Include(s => s.User)
            .Include(s => s.Kit)
            .Include(s => s.Switch)
            .Include(s => s.Keycap)
            .Include(s => s.SoundTests.OrderByDescending(st => st.CreatedAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SpecId == id);

        if (spec == null) return NotFound();

        return View(spec);
    }

    // ── EXPLORE / SEARCH & FILTER ─────────────────────────────────────────────────
    // GET /Spec/Explore?searchString=...&switchType=...

    [HttpGet]
    public async Task<IActionResult> Explore(string? searchString, string? switchType)
    {
        // IQueryable — tổng hợp điều kiện trước, chỉ gọi DB 1 lần
        var query = _db.Specs
            .Include(s => s.User)
            .Include(s => s.Kit)
            .Include(s => s.Switch)
            .Include(s => s.SoundTests)
            .AsNoTracking()
            .AsQueryable();

        // Lọc theo tên build / kit / switch (case-insensitive)
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            var kw = searchString.Trim().ToLower();
            query = query.Where(s =>
                s.BuildName.ToLower().Contains(kw) ||
                s.Kit.Name.ToLower().Contains(kw)  ||
                s.Switch.Name.ToLower().Contains(kw));
        }

        // Lọc theo loại switch
        if (!string.IsNullOrWhiteSpace(switchType))
            query = query.Where(s => s.Switch.Type == switchType);

        var specs = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        // Giữ lại giá trị filter để form hiển thị lại sau submit
        ViewBag.SearchString = searchString;
        ViewBag.SwitchType   = switchType;
        ViewBag.TotalResults = specs.Count;

        return View(specs);
    }
}