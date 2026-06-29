using System.Security.Claims;
using BPCVN.Data;
using BPCVN.Models.Entities;
using BPCVN.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

[Authorize]
public class SoundTestController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IAudioService _audioService;

    // Các định dạng file được phép upload (audio + video)
    private static readonly string[] AllowedExtensions = [".mp3", ".wav", ".flac", ".ogg", ".mp4", ".mov"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public SoundTestController(AppDbContext db, IWebHostEnvironment env, IAudioService audioService)
    {
        _db = db;
        _env = env;
        _audioService = audioService;
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

        // ── Xử lý file (tách âm nếu là video) và lưu trữ ────────────────────
        // Service trả về:
        //   - Audio local  → "/uploads/soundtests/xxx.mp3"   (đường dẫn tương đối)
        //   - Video cloud  → "https://res.cloudinary.com/..." (URL tuyệt đối)
        string audioUrl;
        try
        {
            audioUrl = await _audioService.ProcessAndSaveAsync(audioFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine("====== LỖI RỒI TỨN ƠI: " + ex.ToString());
            ModelState.AddModelError("audioFile", $"Lỗi xử lý file: {ex.Message}");
            ViewBag.Spec = spec;
            return View();
        }

        // ── Lưu DB ───────────────────────────────────────────────────────────
        var soundTest = new SoundTest
        {
            SpecId    = specId,
            MicUsed   = micUsed?.Trim(),
            AudioUrl  = audioUrl,  // Lưu thẳng: tương đối (local) hoặc tuyệt đối (Cloudinary)
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

    // ══════════════════════════════════════════════════════════════════════════
    // LIKE / UNLIKE (AJAX) — Fix spam: mỗi user chỉ like 1 lần
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upvote([FromBody] UpvoteRequest req)
    {
        // Lấy UserId từ cookie claims
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Json(new { success = false, message = "Chưa đăng nhập." });

        // Tìm SoundTest
        var test = await _db.SoundTests.FindAsync(req.TestId);
        if (test == null)
            return Json(new { success = false, message = "Không tìm thấy." });

        // Kiểm tra user đã like chưa
        var existingLike = await _db.SoundTestLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.SoundTestId == req.TestId);

        bool liked;

        if (existingLike != null)
        {
            // ĐÃ CÓ → Unlike: xóa dòng, giảm Upvotes
            _db.SoundTestLikes.Remove(existingLike);
            test.Upvotes = Math.Max(0, test.Upvotes - 1); // Tránh số âm
            liked = false;
        }
        else
        {
            // CHƯA CÓ → Like: thêm dòng, tăng Upvotes
            _db.SoundTestLikes.Add(new SoundTestLike
            {
                UserId = userId,
                SoundTestId = req.TestId
            });
            test.Upvotes++;
            liked = true;
        }

        await _db.SaveChangesAsync();

        return Json(new { success = true, newCount = test.Upvotes, liked });
    }

    // Record nhận JSON body từ Fetch API
    public record UpvoteRequest(Guid TestId);

    // ══════════════════════════════════════════════════════════════════════════
    // COMMENT / REPLY (AJAX) — Bình luận lồng nhau
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lấy danh sách bình luận của một SoundTest (flat list, client tự build tree).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetComments(Guid testId)
    {
        var comments = await _db.SoundTestComments
            .Where(c => c.SoundTestId == testId)
            .Include(c => c.User)
            .OrderBy(c => c.CreatedAt)
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Content,
                createdAt = c.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                c.ParentCommentId,
                userId = c.UserId.ToString(),
                username = c.User.Username
            })
            .ToListAsync();

        return Json(comments);
    }

    /// <summary>
    /// Thêm bình luận mới hoặc reply cho một SoundTest.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment([FromBody] AddCommentRequest req)
    {
        // Lấy UserId từ claims
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Json(new { success = false, message = "Chưa đăng nhập." });

        // Validate nội dung
        if (string.IsNullOrWhiteSpace(req.Content))
            return Json(new { success = false, message = "Nội dung không được để trống." });

        // Kiểm tra SoundTest tồn tại
        var testExists = await _db.SoundTests.AnyAsync(st => st.TestId == req.SoundTestId);
        if (!testExists)
            return Json(new { success = false, message = "Không tìm thấy sound test." });

        // Nếu là reply → kiểm tra comment cha tồn tại
        if (req.ParentCommentId.HasValue)
        {
            var parentExists = await _db.SoundTestComments
                .AnyAsync(c => c.Id == req.ParentCommentId.Value && c.SoundTestId == req.SoundTestId);
            if (!parentExists)
                return Json(new { success = false, message = "Bình luận cha không tồn tại." });
        }

        // Tạo comment mới
        var comment = new SoundTestComment
        {
            Content = req.Content.Trim(),
            UserId = userId,
            SoundTestId = req.SoundTestId,
            ParentCommentId = req.ParentCommentId,
            CreatedAt = DateTime.UtcNow
        };

        _db.SoundTestComments.Add(comment);
        await _db.SaveChangesAsync();

        // Lấy username để trả về cho client hiển thị ngay
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "User";

        return Json(new
        {
            success = true,
            comment = new
            {
                comment.Id,
                comment.Content,
                createdAt = comment.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                comment.ParentCommentId,
                userId = userId.ToString(),
                username
            }
        });
    }

    // Record nhận JSON body cho comment
    public record AddCommentRequest(Guid SoundTestId, string Content, int? ParentCommentId);

    /// <summary>
    /// Xóa bình luận — chỉ chủ comment hoặc Admin.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment([FromBody] DeleteCommentRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Json(new { success = false, message = "Chưa đăng nhập." });

        var comment = await _db.SoundTestComments.FindAsync(req.CommentId);
        if (comment == null)
            return Json(new { success = false, message = "Không tìm thấy bình luận." });

        // Kiểm tra quyền: chủ comment hoặc Admin
        var isOwner = comment.UserId == userId;
        var isAdmin = User.IsInRole("Admin");

        if (!isOwner && !isAdmin)
            return Json(new { success = false, message = "Bạn không có quyền xóa." });

        // Xóa tất cả reply con (đệ quy) trước khi xóa comment cha
        // Vì dùng DeleteBehavior.Restrict nên cần xóa thủ công
        await DeleteCommentAndReplies(comment.Id);
        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// Hàm đệ quy xóa comment và tất cả reply con.
    /// </summary>
    private async Task DeleteCommentAndReplies(int commentId)
    {
        // Tìm tất cả reply trực tiếp của comment này
        var childComments = await _db.SoundTestComments
            .Where(c => c.ParentCommentId == commentId)
            .ToListAsync();

        // Đệ quy xóa từng reply con
        foreach (var child in childComments)
        {
            await DeleteCommentAndReplies(child.Id);
        }

        // Xóa chính comment này
        var comment = await _db.SoundTestComments.FindAsync(commentId);
        if (comment != null)
            _db.SoundTestComments.Remove(comment);
    }

    // Record nhận JSON body cho xóa comment
    public record DeleteCommentRequest(int CommentId);
}