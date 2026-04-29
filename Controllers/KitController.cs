using BPCVN.Data;
using BPCVN.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class KitController : Controller
{
    private readonly AppDbContext _db;

    public KitController(AppDbContext db) => _db = db;

    // GET /Kit — Danh sách tất cả Kit
    public async Task<IActionResult> Index()
    {
        var kits = await _db.Kits
                            .AsNoTracking()
                            .OrderBy(k => k.Brand)
                            .ThenBy(k => k.Name)
                            .ToListAsync();
        return View(kits);
    }

    // GET /Kit/Details/5 — Xem chi tiết một Kit
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var kit = await _db.Kits
                           .AsNoTracking()
                           .FirstOrDefaultAsync(m => m.KitId == id);

        if (kit == null) return NotFound();

        return View(kit);
    }

    // GET /Kit/Edit/5 — Hiển thị form chỉnh sửa Kit
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var kit = await _db.Kits.FindAsync(id);

        if (kit == null) return NotFound();

        return View(kit);
    }

    // POST /Kit/Edit/5 — Xử lý cập nhật Kit vào database
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
}
