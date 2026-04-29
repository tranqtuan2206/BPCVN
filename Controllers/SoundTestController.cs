using System.Security.Claims;
using BPCVN.Data;
using BPCVN.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

[Authorize]
public class SoundTestController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    // Cấu hình upload
    private static readonly string[] AllowedExtensions = [".mp4", ".mp3", ".wav", ".flac", ".ogg"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public SoundTestController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ── UPLOAD GET ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Upload(Guid specId)
    {
        // Kiểm tra spec tồn tại
        var spec = await _db.Specs
                            .Include(s => s.Kit)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.SpecId == specId);

        if (spec == null) return NotFound();

        // Chỉ chủ sở hữu mới được upload
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (spec.UserId.ToString() != userId) return Forbid();

        ViewBag.Spec = spec;
        return View();
    }

    // ── UPLOAD POST ───────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)] // 50MB limit cho request
    public async Task<IActionResult> Upload(Guid specId, IFormFile audioFile, string? micUsed)
    {
        // Lấy lại spec để hiển thị nếu có lỗi
        var spec = await _db.Specs
                            .Include(s => s.Kit)
                            .FirstOrDefaultAsync(s => s.SpecId == specId);

        if (spec == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (spec.UserId.ToString() != userId) return Forbid();

        // ── Validation file ───────────────────────────────────────────────────
        if (audioFile == null || audioFile.Length == 0)
        {
            ModelState.AddModelError("audioFile", "Vui lòng chọn file âm thanh.");
            ViewBag.Spec = spec;
            return View();
        }

        var ext = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            ModelState.AddModelError("audioFile",
                $"Chỉ chấp nhận: {string.Join(", ", AllowedExtensions)}");
            ViewBag.Spec = spec;
            return View();
        }

        if (audioFile.Length > MaxFileSizeBytes)
        {
            ModelState.AddModelError("audioFile", "File không được vượt quá 50MB.");
            ViewBag.Spec = spec;
            return View();
        }

        // ── Lưu file vật lý ──────────────────────────────────────────────────
        var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "soundtests");
        Directory.CreateDirectory(uploadFolder); // tạo nếu chưa có

        // Tên file dùng GUID để tránh trùng và ký tự đặc biệt
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await audioFile.CopyToAsync(stream);

        // ── Lưu DB ───────────────────────────────────────────────────────────
        var soundTest = new SoundTest
        {
            SpecId = specId,
            MicUsed = micUsed?.Trim(),
            AudioUrl = $"/uploads/soundtests/{fileName}",
            CreatedAt = DateTime.UtcNow
        };

        _db.SoundTests.Add(soundTest);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Sound test đã được upload thành công!";
        return RedirectToAction("Details", "Spec", new { id = specId });
    }

    // ── DELETE POST ───────────────────────────────────────────────────────────
    // Xóa một SoundTest — owner của Spec HOẶC Admin

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid testId)
    {
        var soundTest = await _db.SoundTests
            .Include(st => st.Spec)
            .FirstOrDefaultAsync(st => st.TestId == testId);

        if (soundTest == null) return NotFound();

        // Kiểm tra quyền: owner của Spec HOẶC Admin
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOwner = soundTest.Spec.UserId.ToString() == currentUserId;
        var isAdmin = User.IsInRole("Admin");

        if (!isOwner && !isAdmin)
            return Forbid();

        // Xóa file âm thanh vật lý khỏi wwwroot
        var relativePath = soundTest.AudioUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(_env.WebRootPath, relativePath);

        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        var specId = soundTest.SpecId;

        _db.SoundTests.Remove(soundTest);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã xóa sound test thành công.";
        return RedirectToAction("Details", "Spec", new { id = specId });
    }

    // ── UPVOTE (AJAX endpoint) ────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upvote([FromBody] UpvoteRequest req)
    {
        var test = await _db.SoundTests.FindAsync(req.TestId);

        if (test == null)
            return Json(new { success = false, message = "Không tìm thấy." });

        test.Upvotes++;
        await _db.SaveChangesAsync();

        return Json(new { success = true, newCount = test.Upvotes });
    }

    // Record nhận JSON body từ Fetch API
    public record UpvoteRequest(Guid TestId);
}