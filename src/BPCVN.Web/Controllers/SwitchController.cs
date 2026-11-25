using BPCVN.Application.Interfaces;
using BPCVN.Domain.Entities;
using BPCVN.Infrastructure.Data;
using BPCVN.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Web.Controllers;

public class SwitchController : Controller
{
    private readonly AppDbContext _db;
    private readonly ISwitchService _switchService;

    public SwitchController(AppDbContext db, ISwitchService switchService)
    {
        _db = db;
        _switchService = switchService;
    }

    // GET /Switch — Danh sách Switch có phân trang (18 sản phẩm/trang), hỗ trợ AJAX filter + live search
    public async Task<IActionResult> Index(string? searchQuery, string? type, string? brand, [FromQuery] int page = 1)
    {
        var pagedSwitches = await _switchService.GetPagedAsync(searchQuery, type, brand, page);

        // Nếu là AJAX request → trả về Partial View
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_SwitchListPartial", pagedSwitches);

        // Giữ lại giá trị tìm kiếm/bộ lọc hiện tại trên giao diện khi chuyển trang
        ViewBag.SearchQuery = searchQuery;
        ViewBag.SelectedType = type;
        ViewBag.SelectedBrand = brand;

        // Load danh sách type & brand cho dropdown filter
        var (types, brands) = await _switchService.GetFilterOptionsAsync();
        ViewBag.Types = types;
        ViewBag.Brands = brands;

        return View(pagedSwitches);
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

    // ── GET /Switch/Create — Form tạo mới Switch (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View();
    }

    // ── POST /Switch/Create — Xử lý tạo mới Switch (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("SwitchId,Name,Brand,Type,ActuationForce,ImageUrl")] Switch switchObj)
    {
        // Chỉ cho phép URL http(s) hoặc đường dẫn nội bộ an toàn cho ảnh
        if (!string.IsNullOrWhiteSpace(switchObj.ImageUrl) &&
            !MediaSecurityHelper.IsSafeImageUrlOrLocalPath(switchObj.ImageUrl))
        {
            ModelState.AddModelError(nameof(switchObj.ImageUrl), "URL ảnh chỉ được dùng http://, https:// hoặc đường dẫn nội bộ hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            // Trả lại form với lỗi validation
            return View(switchObj);
        }

        _db.Switches.Add(switchObj);
        await _db.SaveChangesAsync();

        TempData["Success"] = "toast.switch.create.success";
        return RedirectToAction(nameof(Details), new { id = switchObj.SwitchId });
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
    public async Task<IActionResult> Edit(int id, [Bind("SwitchId,Name,Brand,Type,ActuationForce,ImageUrl")] Switch switchObj)
    {
        // Đảm bảo id trên URL khớp với SwitchId trong form
        if (id != switchObj.SwitchId) return NotFound();

        // Chỉ cho phép URL http(s) hoặc đường dẫn nội bộ an toàn cho ảnh
        if (!string.IsNullOrWhiteSpace(switchObj.ImageUrl) &&
            !MediaSecurityHelper.IsSafeImageUrlOrLocalPath(switchObj.ImageUrl))
        {
            ModelState.AddModelError(nameof(switchObj.ImageUrl), "URL ảnh chỉ được dùng http://, https:// hoặc đường dẫn nội bộ hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            // Trả lại form với lỗi validation
            return View(switchObj);
        }

        try
        {
            _db.Update(switchObj);
            await _db.SaveChangesAsync();

            TempData["Success"] = "toast.switch.update.success";
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

    // ── POST /Switch/Delete/5 — Xử lý xóa mềm Switch (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var sw = await _db.Switches.FindAsync(id);

        if (sw == null) return NotFound();

        // Xóa mềm: đánh dấu IsDeleted thay vì xóa vật lý
        sw.IsDeleted = true;
        _db.Update(sw);
        await _db.SaveChangesAsync();

        TempData["Success"] = "toast.switch.delete.success";
        return RedirectToAction(nameof(Index));
    }
}
