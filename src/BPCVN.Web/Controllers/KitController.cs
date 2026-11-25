using BPCVN.Application.Interfaces;
using BPCVN.Application.ViewModels;
using BPCVN.Domain.Entities;
using BPCVN.Infrastructure.Data;
using BPCVN.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Web.Controllers;

public class KitController : Controller
{
    private readonly AppDbContext _db;
    private readonly ImageService _imageService;
    private readonly IKitService _kitService;

    public KitController(AppDbContext db, ImageService imageService, IKitService kitService)
    {
        _db = db;
        _imageService = imageService;
        _kitService = kitService;
    }

    // GET /Kit — Danh sách Kit có phân trang (18 sản phẩm/trang), hỗ trợ AJAX filter + live search
    public async Task<IActionResult> Index(string? searchQuery, string? brand, string? layout, [FromQuery] int page = 1)
    {
        var pagedKits = await _kitService.GetPagedAsync(searchQuery, brand, layout, page);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_KitListPartial", pagedKits);

        // Giữ lại giá trị tìm kiếm/bộ lọc hiện tại trên giao diện khi chuyển trang
        ViewBag.SearchQuery = searchQuery;
        ViewBag.SelectedBrand = brand;
        ViewBag.SelectedLayout = layout;

        var (brands, layouts) = await _kitService.GetFilterOptionsAsync();
        ViewBag.Brands = brands;
        ViewBag.Layouts = layouts;

        return View(pagedKits);
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
        // Kiểm tra file ảnh upload và URL ảnh nhập tay trước khi tạo Kit trong DB
        ValidateKitMediaInputs(model.UploadImages, model.ExternalImageUrls);

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

        // Kiểm tra file ảnh upload và URL ảnh nhập tay trước khi cập nhật DB
        ValidateKitMediaInputs(model.UploadImages, model.ExternalImageUrls);

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

    /// <summary>
    /// Kiểm tra hợp lệ cho danh sách file ảnh upload (đuôi, dung lượng, magic bytes)
    /// và danh sách URL ảnh nhập tay (chỉ cho phép http:// hoặc https://).
    /// </summary>
    private void ValidateKitMediaInputs(List<IFormFile>? uploadImages, List<string>? externalUrls)
    {
        if (uploadImages != null)
        {
            foreach (var file in uploadImages)
            {
                if (file == null || file.Length == 0)
                {
                    ModelState.AddModelError("UploadImages", "Có file ảnh trống hoặc không hợp lệ.");
                    continue;
                }

                if (file.Length > MediaSecurityHelper.MaxImageSizeBytes)
                {
                    ModelState.AddModelError("UploadImages", $"Ảnh '{file.FileName}' vượt quá giới hạn 10MB.");
                    continue;
                }

                var ext = MediaSecurityHelper.NormalizeExtension(Path.GetExtension(file.FileName));
                if (!MediaSecurityHelper.IsAllowedImageExtension(ext))
                {
                    ModelState.AddModelError("UploadImages", $"Định dạng ảnh '{ext}' không được hỗ trợ.");
                    continue;
                }

                using var stream = file.OpenReadStream();
                if (!MediaSecurityHelper.IsValidImageSignature(stream, ext))
                    ModelState.AddModelError("UploadImages", $"Ảnh '{file.FileName}' có nội dung không khớp với định dạng.");
            }
        }

        if (externalUrls != null)
        {
            foreach (var rawUrl in externalUrls)
            {
                if (string.IsNullOrWhiteSpace(rawUrl)) continue;
                if (!MediaSecurityHelper.IsSafeHttpUrl(rawUrl))
                    ModelState.AddModelError("ExternalImageUrls", "URL ảnh chỉ được phép dùng giao thức http:// hoặc https://.");
            }
        }
    }
}
