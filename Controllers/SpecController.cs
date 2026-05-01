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
    private readonly IWebHostEnvironment _env;

    public SpecController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

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

        // Lọc theo tên build / kit / switch / customSwitchName (không phân biệt hoa thường)
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            var keyword = searchString.Trim().ToLower();
            query = query.Where(s =>
                s.BuildName.ToLower().Contains(keyword) ||
                s.Kit.Name.ToLower().Contains(keyword)  ||
                (s.Switch != null && s.Switch.Name.ToLower().Contains(keyword)) ||
                (s.CustomSwitchName != null && s.CustomSwitchName.ToLower().Contains(keyword)));
        }

        // Lọc theo loại switch (chỉ áp dụng khi có SwitchId — custom switch không có Type)
        if (!string.IsNullOrWhiteSpace(switchType))
            query = query.Where(s => s.Switch != null && s.Switch.Type == switchType);

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
    public async Task<IActionResult> Create()
    {
        // Truyền danh sách Switch có sẵn cho datalist trên View
        ViewBag.Switches = await _db.Switches
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync();

        return View(new SpecCreateViewModel());
    }

    // ── CREATE POST ───────────────────────────────────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SpecCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Switches = await _db.Switches.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
            return View(vm);
        }

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

        // ── Xử lý Switch — KHÔNG tự tạo Switch mới vào Master Data ──
        // Ưu tiên: SelectedSwitchId > tên trùng DB > CustomSwitchName
        int? switchId = null;
        string? customSwitchName = null;

        if (vm.SelectedSwitchId.HasValue)
        {
            // User chọn Switch có sẵn từ datalist → dùng ID
            switchId = vm.SelectedSwitchId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(vm.SwitchName))
        {
            // User nhập text → kiểm tra xem có trùng tên Switch trong DB không
            var inputName = vm.SwitchName.Trim();
            var existingSwitch = await _db.Switches
                .FirstOrDefaultAsync(s => s.Name.ToLower() == inputName.ToLower());

            if (existingSwitch != null)
            {
                // Tên trùng với Switch có sẵn → tự động map SwitchId
                switchId = existingSwitch.SwitchId;
            }
            else
            {
                // Tên mới hoàn toàn → lưu vào CustomSwitchName
                customSwitchName = inputName;
            }
        }
        else
        {
            // Không nhập gì → báo lỗi
            ModelState.AddModelError("SwitchName", "Vui lòng chọn hoặc nhập tên switch.");
            ViewBag.Switches = await _db.Switches.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
            return View(vm);
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
            UserId           = userId,
            BuildName        = vm.BuildName.Trim(),
            KitId            = kit.KitId,
            SwitchId         = switchId,
            CustomSwitchName = customSwitchName,
            KeycapId         = keycapId,
            PlateMaterial    = vm.PlateMaterial?.Trim(),
            FoamSetup        = vm.FoamSetup?.Trim(),
            Mods             = vm.Mods?.Trim(),
            CreatedAt        = DateTime.UtcNow
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

    // ── EDIT GET ──────────────────────────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var spec = await _db.Specs
            .Include(s => s.Kit)
            .Include(s => s.Switch)
            .Include(s => s.Keycap)
            .FirstOrDefaultAsync(s => s.SpecId == id);

        if (spec == null) return NotFound();

        // Kiểm tra quyền sở hữu — chỉ owner mới được sửa build của mình
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (spec.UserId.ToString() != currentUserId)
            return Forbid();

        // Map sang ViewModel để hiển thị trên form
        var vm = new SpecCreateViewModel
        {
            BuildName        = spec.BuildName,
            KitName          = spec.Kit.Name,
            // Nếu có Switch từ DB → hiển thị tên + ID; nếu custom → hiển thị CustomSwitchName
            SelectedSwitchId = spec.SwitchId,
            SwitchName       = spec.Switch?.Name ?? spec.CustomSwitchName,
            KeycapName       = spec.Keycap?.Name,
            PlateMaterial    = spec.PlateMaterial,
            FoamSetup        = spec.FoamSetup,
            Mods             = spec.Mods
        };

        // Truyền danh sách Switch có sẵn cho datalist
        ViewBag.Switches = await _db.Switches.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
        ViewBag.SpecId = spec.SpecId;
        return View(vm);
    }

    // ── EDIT POST ─────────────────────────────────────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SpecCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Switches = await _db.Switches.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
            ViewBag.SpecId = id;
            return View(vm);
        }

        var spec = await _db.Specs.FindAsync(id);
        if (spec == null) return NotFound();

        // Kiểm tra quyền sở hữu
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (spec.UserId.ToString() != currentUserId)
            return Forbid();

        // Tìm hoặc tạo Kit mới
        var kitName = vm.KitName.Trim();
        var kit = await _db.Kits
            .FirstOrDefaultAsync(k => k.Name.ToLower() == kitName.ToLower());
        if (kit == null)
        {
            kit = new Kit { Name = kitName };
            _db.Kits.Add(kit);
            await _db.SaveChangesAsync();
        }

        // ── Xử lý Switch — KHÔNG tự tạo Switch mới vào Master Data ──
        int? switchId = null;
        string? customSwitchName = null;

        if (vm.SelectedSwitchId.HasValue)
        {
            // User chọn Switch có sẵn từ datalist → dùng ID
            switchId = vm.SelectedSwitchId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(vm.SwitchName))
        {
            // User nhập text → kiểm tra tên trùng DB
            var inputName = vm.SwitchName.Trim();
            var existingSwitch = await _db.Switches
                .FirstOrDefaultAsync(s => s.Name.ToLower() == inputName.ToLower());

            if (existingSwitch != null)
            {
                // Tên trùng → tự động map SwitchId
                switchId = existingSwitch.SwitchId;
            }
            else
            {
                // Tên mới → lưu CustomSwitchName
                customSwitchName = inputName;
            }
        }
        else
        {
            ModelState.AddModelError("SwitchName", "Vui lòng chọn hoặc nhập tên switch.");
            ViewBag.Switches = await _db.Switches.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
            ViewBag.SpecId = id;
            return View(vm);
        }

        // Tìm hoặc tạo Keycap mới (nếu có nhập)
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

        // Cập nhật thông tin Spec
        spec.BuildName        = vm.BuildName.Trim();
        spec.KitId            = kit.KitId;
        spec.SwitchId         = switchId;
        spec.CustomSwitchName = customSwitchName;
        spec.KeycapId         = keycapId;
        spec.PlateMaterial    = vm.PlateMaterial?.Trim();
        spec.FoamSetup        = vm.FoamSetup?.Trim();
        spec.Mods             = vm.Mods?.Trim();

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Build \"{spec.BuildName}\" đã được cập nhật!";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── DELETE POST ───────────────────────────────────────────────────────────
    // Owner được xóa build của mình, Admin được xóa bất kỳ build nào

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var spec = await _db.Specs
            .Include(s => s.SoundTests)
            .FirstOrDefaultAsync(s => s.SpecId == id);

        if (spec == null) return NotFound();

        // Kiểm tra quyền: owner HOẶC Admin
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOwner = spec.UserId.ToString() == currentUserId;
        var isAdmin = User.IsInRole("Admin");

        if (!isOwner && !isAdmin)
            return Forbid();

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
        return RedirectToAction("Profile", "User");
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

        // Lọc theo tên build / kit / switch / customSwitchName (case-insensitive)
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            var kw = searchString.Trim().ToLower();
            query = query.Where(s =>
                s.BuildName.ToLower().Contains(kw) ||
                s.Kit.Name.ToLower().Contains(kw)  ||
                (s.Switch != null && s.Switch.Name.ToLower().Contains(kw)) ||
                (s.CustomSwitchName != null && s.CustomSwitchName.ToLower().Contains(kw)));
        }

        // Lọc theo loại switch (chỉ áp dụng với Switch có trong DB)
        if (!string.IsNullOrWhiteSpace(switchType))
            query = query.Where(s => s.Switch != null && s.Switch.Type == switchType);

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