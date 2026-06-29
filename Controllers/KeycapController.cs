using BPCVN.Data;
using BPCVN.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class KeycapController : Controller
{
    private readonly AppDbContext _db;

    public KeycapController(AppDbContext db) => _db = db;

    // ── GET /Keycap — Danh sách Keycap, hỗ trợ AJAX filter + live search ──
    public async Task<IActionResult> Index(string? searchQuery, string? profile, string? material)
    {
        var query = _db.Keycaps.AsNoTracking().AsQueryable();

        // Lọc theo từ khóa tìm kiếm (tên hoặc brand)
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var kw = searchQuery.Trim().ToLower();
            query = query.Where(k =>
                k.Name.ToLower().Contains(kw) ||
                (k.Brand != null && k.Brand.ToLower().Contains(kw)));
        }

        // Lọc theo profile (Cherry, SA, OEM...)
        if (!string.IsNullOrWhiteSpace(profile))
            query = query.Where(k => k.Profile == profile);

        // Lọc theo chất liệu (PBT, ABS...)
        if (!string.IsNullOrWhiteSpace(material))
            query = query.Where(k => k.Material == material);

        var keycaps = await query
            .OrderBy(k => k.Brand)
            .ThenBy(k => k.Name)
            .ToListAsync();

        // Nếu là AJAX request → trả về Partial View (chỉ HTML danh sách)
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_KeycapListPartial", keycaps);

        // Load danh sách profile & material để đổ vào dropdown filter
        ViewBag.Profiles = await _db.Keycaps
            .Where(k => k.Profile != null)
            .Select(k => k.Profile!)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();

        ViewBag.Materials = await _db.Keycaps
            .Where(k => k.Material != null)
            .Select(k => k.Material!)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        return View(keycaps);
    }

    // ── GET /Keycap/Details/5 — Xem chi tiết một Keycap (public) ──
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var keycap = await _db.Keycaps
                              .AsNoTracking()
                              .FirstOrDefaultAsync(k => k.KeycapId == id);

        if (keycap == null) return NotFound();

        return View(keycap);
    }

    // ── GET /Keycap/Create — Form tạo mới Keycap (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View();
    }

    // ── POST /Keycap/Create — Xử lý tạo mới Keycap (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("KeycapId,Name,Brand,Profile,Material,ImageUrl,Description")] Keycap keycap)
    {
        if (!ModelState.IsValid)
        {
            // Trả lại form với lỗi validation
            return View(keycap);
        }

        _db.Keycaps.Add(keycap);
        await _db.SaveChangesAsync();

        TempData["Success"] = "toast.keycap.create.success";
        return RedirectToAction(nameof(Details), new { id = keycap.KeycapId });
    }

    // ── GET /Keycap/Edit/5 — Form chỉnh sửa Keycap (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var keycap = await _db.Keycaps.FindAsync(id);

        if (keycap == null) return NotFound();

        return View(keycap);
    }

    // ── POST /Keycap/Edit/5 — Xử lý cập nhật Keycap (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("KeycapId,Name,Brand,Profile,Material,ImageUrl,Description")] Keycap keycap)
    {
        // Đảm bảo id trên URL khớp với KeycapId trong form
        if (id != keycap.KeycapId) return NotFound();

        if (!ModelState.IsValid)
        {
            // Trả lại form với lỗi validation
            return View(keycap);
        }

        try
        {
            _db.Update(keycap);
            await _db.SaveChangesAsync();

            TempData["Success"] = "toast.keycap.update.success";
        }
        catch (DbUpdateConcurrencyException)
        {
            // Kiểm tra Keycap có còn tồn tại không
            if (!await _db.Keycaps.AnyAsync(k => k.KeycapId == id))
                return NotFound();

            throw; // Re-throw nếu lỗi khác
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── GET /Keycap/Delete/5 — Trang xác nhận xóa Keycap (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var keycap = await _db.Keycaps
                              .AsNoTracking()
                              .FirstOrDefaultAsync(k => k.KeycapId == id);

        if (keycap == null) return NotFound();

        return View(keycap);
    }

    // ── POST /Keycap/Delete/5 — Xử lý xóa mềm Keycap (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var keycap = await _db.Keycaps.FindAsync(id);

        if (keycap == null) return NotFound();

        // Xóa mềm: đánh dấu IsDeleted thay vì xóa vật lý
        keycap.IsDeleted = true;
        _db.Update(keycap);
        await _db.SaveChangesAsync();

        TempData["Success"] = "toast.keycap.delete.success";
        return RedirectToAction(nameof(Index));
    }
}
