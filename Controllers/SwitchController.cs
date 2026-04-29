using BPCVN.Data;
using BPCVN.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class SwitchController : Controller
{
    private readonly AppDbContext _db;

    public SwitchController(AppDbContext db) => _db = db;

    // GET /Switch — Danh sách tất cả Switch
    public async Task<IActionResult> Index()
    {
        var switches = await _db.Switches
                                .AsNoTracking()
                                .OrderBy(s => s.Type)
                                .ThenBy(s => s.Name)
                                .ToListAsync();
        return View(switches);
    }

    // GET /Switch/Details/5 — Xem chi tiết một Switch
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var sw = await _db.Switches
                          .AsNoTracking()
                          .FirstOrDefaultAsync(s => s.SwitchId == id);

        if (sw == null) return NotFound();

        return View(sw);
    }

    // GET /Switch/Edit/5 — Hiển thị form chỉnh sửa Switch
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var sw = await _db.Switches.FindAsync(id);

        if (sw == null) return NotFound();

        return View(sw);
    }

    // POST /Switch/Edit/5 — Xử lý cập nhật Switch vào database
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
}
