using BPCVN.Data;
using BPCVN.Models.Entities;
using BPCVN.Models.ViewModels;
using BPCVN.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class KitController : Controller
{
    private readonly AppDbContext _db;
    private readonly IImageService _imageService;

    public KitController(AppDbContext db, IImageService imageService)
    {
        _db = db;
        _imageService = imageService;
    }

    // GET /Kit — Danh sách Kit, hỗ trợ AJAX filter + live search
    public async Task<IActionResult> Index(string? searchQuery, string? brand, string? layout)
    {
        var query = _db.Kits.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var kw = searchQuery.Trim().ToLower();
            query = query.Where(k =>
                k.Name.ToLower().Contains(kw) ||
                (k.Brand != null && k.Brand.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(brand))
            query = query.Where(k => k.Brand == brand);

        if (!string.IsNullOrWhiteSpace(layout))
            query = query.Where(k => k.Layout == layout);

        var kits = await query
            .Include(k => k.KitImages) // Load ảnh kèm Kit
            .OrderBy(k => k.Brand)
            .ThenBy(k => k.Name)
            .ToListAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_KitListPartial", kits);

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
                           .Include(k => k.KitImages.OrderBy(i => i.SortOrder))
                           .FirstOrDefaultAsync(m => m.KitId == id);

        if (kit == null) return NotFound();

        return View(kit);
    }

    // ── GET /Kit/Create — Form tạo mới Kit (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View(new KitCreateViewModel());
    }

    // ── POST /Kit/Create — Xử lý tạo mới Kit (chỉ Admin) ──
    // Gộp dữ liệu từ 2 nguồn: UploadImages (Cloudinary) + ExternalImageUrls (nhập tay)
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KitCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // ── BƯỚC 1: Tạo entity Kit từ ViewModel ────────────────────────────
        var kit = new Kit
        {
            Name = model.Name,
            Brand = model.Brand,
            Layout = model.Layout,
            MountType = model.MountType,
            PcbType = model.PcbType
        };

        _db.Kits.Add(kit);
        await _db.SaveChangesAsync(); // Save để có KitId dùng cho KitImage

        // ── BƯỚC 2: Thu thập URL ảnh + ColorHex từ 2 nguồn ─────────────────
        var imageData = new List<(string Url, string? Color)>();

        // Nguồn A: Upload file lên Cloudinary
        if (model.UploadImages != null && model.UploadImages.Count > 0)
        {
            var uploadedUrls = await _imageService.UploadImagesAsync(model.UploadImages);
            for (int i = 0; i < uploadedUrls.Count; i++)
            {
                // Lấy màu — nếu "Không màu" hoặc rỗng thì lưu null
                var color = (model.UploadImageColors != null && i < model.UploadImageColors.Count)
                    ? model.UploadImageColors[i]
                    : null;
                imageData.Add((uploadedUrls[i], string.IsNullOrWhiteSpace(color) ? null : color));
            }
        }

        // Nguồn B: External URL nhập tay (lọc bỏ null/rỗng)
        if (model.ExternalImageUrls != null)
        {
            for (int i = 0; i < model.ExternalImageUrls.Count; i++)
            {
                var url = model.ExternalImageUrls[i];
                if (string.IsNullOrWhiteSpace(url)) continue;

                var color = (model.ExternalImageColors != null && i < model.ExternalImageColors.Count)
                    ? model.ExternalImageColors[i]
                    : null;
                imageData.Add((url.Trim(), string.IsNullOrWhiteSpace(color) ? null : color));
            }
        }

        // ── BƯỚC 3: Tạo KitImage entities và lưu vào DB ────────────────────
        if (imageData.Count > 0)
        {
            var kitImages = imageData.Select((img, index) => new KitImage
            {
                KitId = kit.KitId,
                ImageUrl = img.Url,
                SortOrder = index,
                ColorHex = img.Color
            }).ToList();

            _db.KitImages.AddRange(kitImages);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "toast.kit.create.success";
        return RedirectToAction(nameof(Details), new { id = kit.KitId });
    }

    // GET /Kit/Edit/5 — Chỉ Admin được chỉnh sửa Kit
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var kit = await _db.Kits
                           .Include(k => k.KitImages.OrderBy(i => i.SortOrder))
                           .FirstOrDefaultAsync(k => k.KitId == id);

        if (kit == null) return NotFound();

        // Map từ entity → ViewModel, giữ lại danh sách ảnh hiện có
        var model = new KitEditViewModel
        {
            KitId = kit.KitId,
            Name = kit.Name,
            Brand = kit.Brand,
            Layout = kit.Layout,
            MountType = kit.MountType,
            PcbType = kit.PcbType
            // ExternalImageUrls + colors sẽ được truyền qua ViewBag.ExistingImages
        };

        // Truyền danh sách ảnh hiện có (bao gồm ColorHex) qua ViewBag
        ViewBag.ExistingImages = kit.KitImages.ToList();

        return View(model);
    }

    // ── POST /Kit/Edit/5 — Đồng bộ ảnh bằng Id ──
    // So sánh ExistingImageIds gửi lên với DB:
    //   - Ảnh có trong DB nhưng KHÔNG có Id trong form → XÓA
    //   - Ảnh có Id khớp → CẬP NHẬT ColorHex + SortOrder
    //   - Ảnh mới (Id=0) → THÊM MỚI
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, KitEditViewModel model)
    {
        if (id != model.KitId) return NotFound();

        if (!ModelState.IsValid)
        {
            var existingKitForRetry = await _db.Kits
                                               .Include(k => k.KitImages)
                                               .FirstOrDefaultAsync(k => k.KitId == id);
            ViewBag.ExistingImages = existingKitForRetry?.KitImages.OrderBy(i => i.SortOrder).ToList();
            return View(model);
        }

        var kit = await _db.Kits.FindAsync(id);
        if (kit == null) return NotFound();

        try
        {
            // ── Cập nhật thông tin Kit ──────────────────────────────
            kit.Name = model.Name;
            kit.Brand = model.Brand;
            kit.Layout = model.Layout;
            kit.MountType = model.MountType;
            kit.PcbType = model.PcbType;
            _db.Update(kit);

            // ── Lấy toàn bộ ảnh hiện có trong DB ──────────────────
            var allDbImages = await _db.KitImages
                .Where(ki => ki.KitId == id)
                .ToListAsync();

            // Tập Id ảnh cũ mà user giữ lại (theo đúng thứ tự kéo thả)
            var submittedIds = model.ExistingImageIds ?? new List<int>();
            var submittedIdSet = submittedIds.ToHashSet();

            // ── BƯỚC 1: XÓA ảnh bị user bỏ ────────────────────────
            // Ảnh có trong DB nhưng Id không nằm trong submittedIds → xóa
            var imagesToRemove = allDbImages
                .Where(dbImg => !submittedIdSet.Contains(dbImg.Id))
                .ToList();
            _db.KitImages.RemoveRange(imagesToRemove);

            // ── BƯỚC 2: CẬP NHẬT ảnh cũ giữ lại ──────────────────
            // Duyệt theo thứ tự submittedIds để gán SortOrder chuẩn
            var existingColors = model.ExistingImageColors ?? new List<string>();
            for (int sortIdx = 0; sortIdx < submittedIds.Count; sortIdx++)
            {
                var imgId = submittedIds[sortIdx];
                var dbImg = allDbImages.FirstOrDefault(i => i.Id == imgId);
                if (dbImg == null) continue;

                // Lấy màu tương ứng từ ExistingImageColors
                var color = (sortIdx < existingColors.Count) ? existingColors[sortIdx] : null;
                dbImg.ColorHex = string.IsNullOrWhiteSpace(color) ? null : color;
                dbImg.SortOrder = sortIdx;
                _db.Update(dbImg);
            }

            // ── BƯỚC 3: Upload file mới lên Cloudinary ─────────────
            var nextSortOrder = submittedIds.Count;
            if (model.UploadImages != null && model.UploadImages.Count > 0)
            {
                var uploadedUrls = await _imageService.UploadImagesAsync(model.UploadImages);
                var uploadColors = model.UploadImageColors ?? new List<string>();

                for (int i = 0; i < uploadedUrls.Count; i++)
                {
                    var color = (i < uploadColors.Count) ? uploadColors[i] : null;
                    _db.KitImages.Add(new KitImage
                    {
                        KitId = id,
                        ImageUrl = uploadedUrls[i],
                        SortOrder = nextSortOrder++,
                        ColorHex = string.IsNullOrWhiteSpace(color) ? null : color
                    });
                }
            }

            // ── BƯỚC 4: Thêm URL mới nhập tay ─────────────────────
            if (model.ExternalImageUrls != null)
            {
                var urlColors = model.ExternalImageColors ?? new List<string>();

                for (int i = 0; i < model.ExternalImageUrls.Count; i++)
                {
                    var url = model.ExternalImageUrls[i];
                    if (string.IsNullOrWhiteSpace(url)) continue;

                    var color = (i < urlColors.Count) ? urlColors[i] : null;
                    _db.KitImages.Add(new KitImage
                    {
                        KitId = id,
                        ImageUrl = url.Trim(),
                        SortOrder = nextSortOrder++,
                        ColorHex = string.IsNullOrWhiteSpace(color) ? null : color
                    });
                }
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "toast.kit.update.success";
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Kits.AnyAsync(k => k.KitId == id))
                return NotFound();
            throw;
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
                           .Include(k => k.KitImages)
                           .FirstOrDefaultAsync(k => k.KitId == id);

        if (kit == null) return NotFound();

        return View(kit);
    }

    // ── POST /Kit/Delete/5 — Xử lý xóa mềm Kit (chỉ Admin) ──
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var kit = await _db.Kits.FindAsync(id);

        if (kit == null) return NotFound();

        kit.IsDeleted = true;
        _db.Update(kit);
        await _db.SaveChangesAsync();

        TempData["Success"] = "toast.kit.delete.success";
        return RedirectToAction(nameof(Index));
    }
}
