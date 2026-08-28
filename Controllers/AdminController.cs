using System.Security.Claims;
using BPCVN.Data;
using BPCVN.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

/// <summary>
/// Quản lý trang Admin: duyệt / từ chối / gộp Kit/Switch/Keycap do user tự tạo.
/// Tất cả action đều yêu cầu role "Admin".
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // ── PENDING QUEUE ─────────────────────────────────────────────────────────
    // GET /Admin/Pending — Hiển thị tất cả item đang chờ duyệt, group theo Type

    [HttpGet]
    public async Task<IActionResult> Pending()
    {
        var items = await _db.PendingItems
            .Include(p => p.SubmittedBy)
            .Where(p => p.Status == PendingStatus.Pending)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.SubmittedAt)
            .AsNoTracking()
            .ToListAsync();

        return View(items);
    }

    // ── APPROVE ───────────────────────────────────────────────────────────────
    // POST /Admin/Approve/{id} — Duyệt item: set entity IsApproved=true

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var pending = await _db.PendingItems.FindAsync(id);
        if (pending == null) return NotFound();

        // Cập nhật entity tương ứng thành IsApproved = true
        var success = await SetEntityApproved(pending.Type, pending.EntityId, true);
        if (!success)
        {
            TempData["Error"] = "Không tìm thấy entity để duyệt.";
            return RedirectToAction(nameof(Pending));
        }

        pending.Status = PendingStatus.Approved;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã duyệt {pending.Type}: \"{pending.Name}\"";
        return RedirectToAction(nameof(Pending));
    }

    // ── REJECT ────────────────────────────────────────────────────────────────
    // POST /Admin/Reject/{id} — Từ chối: xóa entity pending, Spec fallback về CustomSwitchName

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var pending = await _db.PendingItems.FindAsync(id);
        if (pending == null) return NotFound();

        if (pending.EntityId.HasValue)
        {
            // Trước khi xóa entity: cập nhật các Spec đang dùng entity này về null FK
            // Codebase đã có fallback hiển thị tên từ CustomSwitchName khi FK null
            await NullifyEntityReferences(pending.Type, pending.EntityId.Value, pending.Name);
        }

        pending.Status   = PendingStatus.Rejected;
        pending.EntityId = null; // Entity đã bị xóa
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã từ chối {pending.Type}: \"{pending.Name}\"";
        return RedirectToAction(nameof(Pending));
    }

    // ── MERGE ─────────────────────────────────────────────────────────────────
    // POST /Admin/Merge — Gộp entity rác vào entity chuẩn đã có sẵn
    // targetId: Id của entity chuẩn (approved) cần giữ lại

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Merge(int id, int targetId)
    {
        var pending = await _db.PendingItems.FindAsync(id);
        if (pending == null) return NotFound();

        if (!pending.EntityId.HasValue)
        {
            TempData["Error"] = "Entity pending đã bị xóa, không thể merge.";
            return RedirectToAction(nameof(Pending));
        }

        // Cập nhật tất cả Spec đang dùng entity rác → sang entity chuẩn
        await RewireEntityReferences(pending.Type, pending.EntityId.Value, targetId);

        // Xóa entity rác khỏi bảng chính
        await DeletePendingEntity(pending.Type, pending.EntityId.Value);

        pending.Status   = PendingStatus.Merged;
        pending.EntityId = null; // Entity rác đã bị xóa
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã gộp {pending.Type}: \"{pending.Name}\" vào entity #{targetId}";
        return RedirectToAction(nameof(Pending));
    }

    // ── MERGE TARGETS API ─────────────────────────────────────────────────────
    // GET /Admin/MergeTargets?type=Switch — Trả JSON list entity approved để chọn merge target

    [HttpGet]
    public async Task<IActionResult> MergeTargets(string type)
    {
        // Chỉ trả entity đã được duyệt (IsApproved=true)
        var results = type switch
        {
            "Switch" => await _db.Switches.IgnoreQueryFilters()
                .Where(s => s.IsApproved && !s.IsDeleted)
                .Select(s => new { id = s.SwitchId, name = s.Name })
                .OrderBy(s => s.name)
                .ToListAsync<object>(),

            "Kit" => await _db.Kits.IgnoreQueryFilters()
                .Where(k => k.IsApproved && !k.IsDeleted)
                .Select(k => new { id = k.KitId, name = k.Name })
                .OrderBy(k => k.name)
                .ToListAsync<object>(),

            "Keycap" => await _db.Keycaps.IgnoreQueryFilters()
                .Where(k => k.IsApproved && !k.IsDeleted)
                .Select(k => new { id = k.KeycapId, name = k.Name })
                .OrderBy(k => k.name)
                .ToListAsync<object>(),

            _ => new List<object>()
        };

        return Json(results);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Set IsApproved trên entity theo Type và EntityId.</summary>
    private async Task<bool> SetEntityApproved(string type, int? entityId, bool approved)
    {
        if (!entityId.HasValue) return false;

        switch (type)
        {
            case "Switch":
                // IgnoreQueryFilters + FirstOrDefaultAsync (FindAsync không work sau IgnoreQueryFilters)
                var sw = await _db.Switches.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.SwitchId == entityId.Value);
                if (sw == null) return false;
                sw.IsApproved = approved;
                break;

            case "Kit":
                var kit = await _db.Kits.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(k => k.KitId == entityId.Value);
                if (kit == null) return false;
                kit.IsApproved = approved;
                break;

            case "Keycap":
                var keycap = await _db.Keycaps.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(k => k.KeycapId == entityId.Value);
                if (keycap == null) return false;
                keycap.IsApproved = approved;
                break;

            default:
                return false;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Khi Reject: set FK về null trên tất cả Spec đang dùng entity bị xóa.
    /// Copy tên vào CustomSwitchName (chỉ cho Switch) để Spec vẫn hiển thị tên.
    /// Sau đó xóa entity khỏi bảng chính.
    /// </summary>
    private async Task NullifyEntityReferences(string type, int entityId, string entityName)
    {
        switch (type)
        {
            case "Switch":
                // Set SwitchId = null, copy tên vào CustomSwitchName để giữ tên hiển thị
                var switchSpecs = await _db.Specs
                    .Where(s => s.SwitchId == entityId)
                    .ToListAsync();
                foreach (var spec in switchSpecs)
                {
                    spec.SwitchId         = null;
                    spec.CustomSwitchName = entityName; // Giữ tên hiển thị
                }
                await _db.SaveChangesAsync();

                var sw = await _db.Switches.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.SwitchId == entityId);
                if (sw != null) _db.Switches.Remove(sw);
                break;

            case "Kit":
                // Kit không thể null (FK required) → giữ nguyên, chỉ set IsDeleted=true
                var kit = await _db.Kits.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(k => k.KitId == entityId);
                if (kit != null) { kit.IsDeleted = true; kit.IsApproved = false; }
                break;

            case "Keycap":
                // Set KeycapId = null trên tất cả Spec
                var keycapSpecs = await _db.Specs
                    .Where(s => s.KeycapId == entityId)
                    .ToListAsync();
                foreach (var spec in keycapSpecs)
                    spec.KeycapId = null;
                await _db.SaveChangesAsync();

                var keycap = await _db.Keycaps.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(k => k.KeycapId == entityId);
                if (keycap != null) _db.Keycaps.Remove(keycap);
                break;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Khi Merge: cập nhật tất cả Spec từ entity rác (sourceId) sang entity chuẩn (targetId).
    /// </summary>
    private async Task RewireEntityReferences(string type, int sourceId, int targetId)
    {
        switch (type)
        {
            case "Switch":
                var switchSpecs = await _db.Specs
                    .Where(s => s.SwitchId == sourceId)
                    .ToListAsync();
                foreach (var spec in switchSpecs)
                    spec.SwitchId = targetId;
                break;

            case "Kit":
                var kitSpecs = await _db.Specs
                    .Where(s => s.KitId == sourceId)
                    .ToListAsync();
                foreach (var spec in kitSpecs)
                    spec.KitId = targetId;
                break;

            case "Keycap":
                var keycapSpecs = await _db.Specs
                    .Where(s => s.KeycapId == sourceId)
                    .ToListAsync();
                foreach (var spec in keycapSpecs)
                    spec.KeycapId = targetId;
                break;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>Xóa entity khỏi bảng chính sau khi đã Merge.</summary>
    private async Task DeletePendingEntity(string type, int entityId)
    {
        switch (type)
        {
            case "Switch":
                var sw = await _db.Switches.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.SwitchId == entityId);
                if (sw != null) _db.Switches.Remove(sw);
                break;

            case "Kit":
                var kit = await _db.Kits.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(k => k.KitId == entityId);
                if (kit != null) _db.Kits.Remove(kit);
                break;

            case "Keycap":
                var keycap = await _db.Keycaps.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(k => k.KeycapId == entityId);
                if (keycap != null) _db.Keycaps.Remove(keycap);
                break;
        }

        await _db.SaveChangesAsync();
    }
}
