using BPCVN.Data;
using BPCVN.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class SwitchController : Controller
{
    private readonly AppDbContext _db;

    public SwitchController(AppDbContext db) => _db = db;

    // GET /Switch — Danh sách Switch, hỗ trợ AJAX filter + live search
    public async Task<IActionResult> Index(string? searchQuery, string? type, string? brand)
    {
        var query = _db.Switches.AsNoTracking().AsQueryable();

        // Lọc theo từ khóa tìm kiếm (tên hoặc brand)
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var kw = searchQuery.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(kw) ||
                (s.Brand != null && s.Brand.ToLower().Contains(kw)));
        }

        // Lọc theo loại switch (Linear / Tactile / Clicky)
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(s => s.Type == type);

        // Lọc theo brand
        if (!string.IsNullOrWhiteSpace(brand))
            query = query.Where(s => s.Brand == brand);

        var switches = await query
            .OrderBy(s => s.Type)
            .ThenBy(s => s.Name)
            .ToListAsync();

        // Nếu là AJAX request → trả về Partial View
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_SwitchListPartial", switches);

        // Load danh sách type & brand cho dropdown filter
        ViewBag.Types = await _db.Switches
            .Where(s => s.Type != null)
            .Select(s => s.Type!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

        ViewBag.Brands = await _db.Switches
            .Where(s => s.Brand != null)
            .Select(s => s.Brand!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

        return View(switches);
    }

    // GET /Switch/Details/5 — Xem chi tiết một Switch (public)
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var sw = await _db.Switches
                          .AsNoTracking()
                          .FirstOrDefaultAsync(s => s.SwitchId == id);

        if (sw == null) return NotFound();

        return View(sw);
    }

    // GET /Switch/Edit/5 — Chỉ Admin được chỉnh sửa Switch
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var sw = await _db.Switches.FindAsync(id);

        if (sw == null) return NotFound();

        return View(sw);
    }

    // POST /Switch/Edit/5 — Chỉ Admin được cập nhật Switch
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Switch switchObj)
    {
        // Đảm bảo id trên URL khớp với SwitchId trong form
        if (id != switchObj.SwitchId) return NotFound();

        if (!ModelState.IsValid)
        {
            // Trả lại form với lỗi validation
            return View(switchObj);
        }

        try
        {
            _db.Update(switchObj);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Cập nhật Switch thành công!";
        }
        catch (DbUpdateConcurrencyException)
        {
            // Kiểm tra Switch có còn tồn tại không
            if (!await _db.Switches.AnyAsync(s => s.SwitchId == id))
                return NotFound();

            throw; // Re-throw nếu lỗi khác
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── GET /Switch/Delete/5 — Trang xác nhận xóa Switch (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var sw = await _db.Switches
                          .AsNoTracking()
                          .FirstOrDefaultAsync(s => s.SwitchId == id);

        if (sw == null) return NotFound();

        return View(sw);
    }

    // ── POST /Switch/Delete/5 — Xử lý xóa Switch (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var sw = await _db.Switches.FindAsync(id);

        if (sw == null) return NotFound();

        _db.Switches.Remove(sw);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã xóa Switch thành công!";
        return RedirectToAction(nameof(Index));
    }
}
