using BPCVN.Data;
using BPCVN.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class KitController : Controller
{
    private readonly AppDbContext _db;

    public KitController(AppDbContext db) => _db = db;

    // GET /Kit — Danh sách Kit, hỗ trợ AJAX filter + live search
    public async Task<IActionResult> Index(string? searchQuery, string? brand, string? layout)
    {
        var query = _db.Kits.AsNoTracking().AsQueryable();

        // Lọc theo từ khóa tìm kiếm (tên hoặc brand)
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var kw = searchQuery.Trim().ToLower();
            query = query.Where(k =>
                k.Name.ToLower().Contains(kw) ||
                (k.Brand != null && k.Brand.ToLower().Contains(kw)));
        }

        // Lọc theo brand
        if (!string.IsNullOrWhiteSpace(brand))
            query = query.Where(k => k.Brand == brand);

        // Lọc theo layout
        if (!string.IsNullOrWhiteSpace(layout))
            query = query.Where(k => k.Layout == layout);

        var kits = await query
            .OrderBy(k => k.Brand)
            .ThenBy(k => k.Name)
            .ToListAsync();

        // Nếu là AJAX request → trả về Partial View (chỉ HTML danh sách)
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_KitListPartial", kits);

        // Load danh sách brand & layout để đổ vào dropdown filter
        ViewBag.Brands = await _db.Kits
            .Where(k => k.Brand != null)
            .Select(k => k.Brand!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

        ViewBag.Layouts = await _db.Kits
            .Where(k => k.Layout != null)
            .Select(k => k.Layout!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

        return View(kits);
    }

    // GET /Kit/Details/5 — Xem chi tiết một Kit (public)
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var kit = await _db.Kits
                           .AsNoTracking()
                           .FirstOrDefaultAsync(m => m.KitId == id);

        if (kit == null) return NotFound();

        return View(kit);
    }

    // GET /Kit/Edit/5 — Chỉ Admin được chỉnh sửa Kit
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var kit = await _db.Kits.FindAsync(id);

        if (kit == null) return NotFound();

        return View(kit);
    }

    // POST /Kit/Edit/5 — Chỉ Admin được cập nhật Kit
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Kit kit)
    {
        // Đảm bảo id trên URL khớp với KitId trong form
        if (id != kit.KitId) return NotFound();

        if (!ModelState.IsValid)
        {
            // Trả lại form với lỗi validation
            return View(kit);
        }

        try
        {
            _db.Update(kit);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Cập nhật Kit thành công!";
        }
        catch (DbUpdateConcurrencyException)
        {
            // Kiểm tra Kit có còn tồn tại không
            if (!await _db.Kits.AnyAsync(k => k.KitId == id))
                return NotFound();

            throw; // Re-throw nếu lỗi khác
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── GET /Kit/Delete/5 — Trang xác nhận xóa Kit (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var kit = await _db.Kits
                           .AsNoTracking()
                           .FirstOrDefaultAsync(k => k.KitId == id);

        if (kit == null) return NotFound();

        return View(kit);
    }

    // ── POST /Kit/Delete/5 — Xử lý xóa Kit (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var kit = await _db.Kits.FindAsync(id);

        if (kit == null) return NotFound();

        _db.Kits.Remove(kit);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã xóa Kit thành công!";
        return RedirectToAction(nameof(Index));
    }
}
