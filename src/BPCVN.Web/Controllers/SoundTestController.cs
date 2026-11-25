using System.Security.Claims;
using BPCVN.Application.DTOs;
using BPCVN.Domain.Entities;
using BPCVN.Infrastructure.Data;
using BPCVN.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Web.Controllers;

[Authorize]
public class SoundTestController : Controller
{
    private readonly AppDbContext _db;
    private readonly AudioService _audioService;
    private readonly IWebHostEnvironment  _env;
    private readonly ILogger<SoundTestController> _logger;

    // Loại file được chấp nhận — dùng chung chuẩn bảo mật từ MediaSecurityHelper
    private static readonly HashSet<string> AllowedExtensions = MediaSecurityHelper.AllowedMediaExtensions;
    private const long MaxAudioSizeBytes = MediaSecurityHelper.MaxAudioSizeBytes; // 100 MB
    private const long MaxVideoSizeBytes = MediaSecurityHelper.MaxVideoSizeBytes; // 200 MB

    public SoundTestController(AppDbContext db, IWebHostEnvironment env,
        AudioService audioService, ILogger<SoundTestController> logger)
    {
        _db           = db;
        _env          = env;
        _audioService = audioService;
        _logger       = logger;
    }

    // ── UPLOAD GET ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Upload(Guid specId)
    {
        // Kiểm tra spec tồn tại
        var spec = await _db.Specs
                            .IgnoreQueryFilters()
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
    [RequestSizeLimit(200 * 1024 * 1024)] // 200MB — cho phép upload video lớn từ điện thoại
    public async Task<IActionResult> Upload(Guid specId, IFormFile audioFile, string? micUsed)
    {
        // Lấy lại spec để hiển thị nếu có lỗi
        var spec = await _db.Specs
                            .IgnoreQueryFilters()
                            .Include(s => s.Kit)
                            .FirstOrDefaultAsync(s => s.SpecId == specId);

        if (spec == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (spec.UserId.ToString() != userId) return Forbid();

        // Xác định request có phải AJAX không
        // Check header X-Requested-With (desktop) HOẶC hidden field isAjax (mobile fallback)
        bool isAjax = Request.Headers.XRequestedWith == "XMLHttpRequest"
                      || Request.Form.ContainsKey("isAjax");

        // ── Validation file ───────────────────────────────────────────────────
        if (audioFile == null || audioFile.Length == 0)
        {
            if (isAjax) return Json(new { success = false, message = "Vui lòng chọn file âm thanh." });
            ModelState.AddModelError("audioFile", "Vui lòng chọn file âm thanh.");
            ViewBag.Spec = spec;
            return View();
        }

        var ext = MediaSecurityHelper.NormalizeExtension(Path.GetExtension(audioFile.FileName));
        if (!AllowedExtensions.Contains(ext))
        {
            var msg = $"Chỉ chấp nhận: {string.Join(", ", AllowedExtensions)}";
            if (isAjax) return Json(new { success = false, message = msg });
            ModelState.AddModelError("audioFile", msg);
            ViewBag.Spec = spec;
            return View();
        }

        // Kiểm tra dung lượng theo loại file (audio nhỏ hơn, video lớn hơn)
        var isVideo = MediaSecurityHelper.IsVideoExtension(ext);
        var maxSize = isVideo ? MaxVideoSizeBytes : MaxAudioSizeBytes;
        var maxSizeLabel = isVideo ? "200MB" : "100MB";

        if (audioFile.Length > maxSize)
        {
            var msg = $"File không được vượt quá {maxSizeLabel}.";
            if (isAjax) return Json(new { success = false, message = msg });
            ModelState.AddModelError("audioFile", msg);
            ViewBag.Spec = spec;
            return View();
        }

        // Kiểm tra chữ ký đầu file (magic bytes) để chặn đổi đuôi giả mạo
        await using (var sigStream = audioFile.OpenReadStream())
        {
            if (!MediaSecurityHelper.IsValidMediaSignature(sigStream, ext))
            {
                const string sigMsg = "Nội dung file không khớp với định dạng khai báo (nghi ngờ đổi đuôi giả mạo).";
                if (isAjax) return Json(new { success = false, message = sigMsg });
                ModelState.AddModelError("audioFile", sigMsg);
                ViewBag.Spec = spec;
                return View();
            }
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
            // Dùng logger thay vì Console.WriteLine để tích hợp với ASP.NET logging pipeline
            _logger.LogError(ex, "[SoundTest] Lỗi xử lý file upload cho Spec {SpecId}", specId);
            if (isAjax) return Json(new { success = false, message = $"Lỗi xử lý file: {ex.Message}" });
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

        // AJAX: trả về URL redirect để client tự chuyển hướng
        if (isAjax) return Json(new { success = true, redirectUrl = Url.Action("Details", "Spec", new { id = specId }) });

        TempData["Success"] = "toast.soundtest.upload.success";
        return RedirectToAction("Details", "Spec", new { id = specId });
    }

    // ── CHUNKED UPLOAD — Fix iOS Safari upload file lớn ──────────────────────
    // Chia file thành chunks 18MB trên client, gửi từng chunk lên server,
    // server ghép lại rồi xử lý bằng FFmpeg như cũ.

    private const long ChunkSizeBytes = 18 * 1024 * 1024; // 18MB mỗi chunk
    private const string ChunkTempRelPath = "App_Data/temp/chunks";

    /// <summary>
    /// Nhận 1 chunk file-upload từ client. Lưu cô lập theo userId trong App_Data/temp/chunks/{userId}/{uploadId}.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20MB — cho phép chunk 18MB + overhead
    public async Task<IActionResult> UploadChunk(
        Guid uploadId, int chunkIndex, int totalChunks,
        Guid specId, string? micUsed, IFormFile chunk)
    {
        // Xác thực spec tồn tại + quyền sở hữu
        var spec = await _db.Specs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.SpecId == specId);
        if (spec == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || spec.UserId.ToString() != userId) return Forbid();

        // Kiểm tra biên số chunk để chống Path Traversal / spam tạo hàng nghìn file
        if (totalChunks < 1 || totalChunks > MediaSecurityHelper.MaxChunkCount ||
            chunkIndex < 0 || chunkIndex >= totalChunks)
        {
            return Json(new { success = false, message = "Thông số chunk không hợp lệ." });
        }

        // Validate chunk
        if (chunk == null || chunk.Length == 0)
            return Json(new { success = false, message = "Chunk trống." });

        // Lưu trong App_Data (ngoài wwwroot) và cô lập theo userId để tránh IDOR uploadId
        var sessionDir = Path.Combine(_env.ContentRootPath, "App_Data", "temp", "chunks", userId, uploadId.ToString());
        Directory.CreateDirectory(sessionDir);

        // Lưu chunk vào file tạm
        var chunkPath = Path.Combine(sessionDir, $"chunk_{chunkIndex}");
        await using (var stream = new FileStream(chunkPath, FileMode.Create))
        {
            await chunk.CopyToAsync(stream);
        }

        _logger.LogInformation("[ChunkedUpload] Đã nhận chunk {Index}/{Total} cho upload {UploadId}",
            chunkIndex + 1, totalChunks, uploadId);

        // Trả về số chunk đã nhận — client dùng để cập nhật progress
        return Json(new { success = true, receivedChunks = chunkIndex + 1, totalChunks });
    }

    /// <summary>
    /// Ghép tất cả chunks lại thành file hoàn chỉnh, kiểm tra định dạng/dung lượng/magic bytes rồi xử lý bằng AudioService.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizeUpload(
        Guid uploadId, Guid specId, string? micUsed,
        string originalFileName, int totalChunks)
    {
        // Xác thực spec + quyền
        var spec = await _db.Specs.IgnoreQueryFilters()
            .Include(s => s.Kit)
            .FirstOrDefaultAsync(s => s.SpecId == specId);
        if (spec == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || spec.UserId.ToString() != userId) return Forbid();

        // Kiểm tra tổng số chunk hợp lệ
        if (totalChunks < 1 || totalChunks > MediaSecurityHelper.MaxChunkCount)
            return Json(new { success = false, message = "Số lượng chunk không hợp lệ." });

        // Kiểm tra phần mở rộng từ originalFileName ngay trước khi tạo file ghép
        var ext = MediaSecurityHelper.NormalizeExtension(Path.GetExtension(originalFileName));
        if (!MediaSecurityHelper.IsAllowedMediaExtension(ext))
        {
            return Json(new
            {
                success = false,
                message = $"Chỉ chấp nhận: {string.Join(", ", AllowedExtensions)}"
            });
        }

        var sessionDir = Path.Combine(_env.ContentRootPath, "App_Data", "temp", "chunks", userId, uploadId.ToString());

        // Kiểm tra đủ số chunk
        if (!Directory.Exists(sessionDir))
            return Json(new { success = false, message = "Không tìm thấy session upload." });

        var isVideo = MediaSecurityHelper.IsVideoExtension(ext);
        var maxAllowedSize = isVideo ? MaxVideoSizeBytes : MaxAudioSizeBytes;
        var mergedPath = Path.Combine(sessionDir, $"merged{ext}");

        try
        {
            long totalMergedBytes = 0;

            // Ghép chunks theo thứ tự chunk_0, chunk_1, chunk_2... và kiểm tra tổng dung lượng cộng dồn
            await using (var mergedStream = new FileStream(mergedPath, FileMode.Create))
            {
                for (int i = 0; i < totalChunks; i++)
                {
                    var chunkPath = Path.Combine(sessionDir, $"chunk_{i}");
                    if (!System.IO.File.Exists(chunkPath))
                        return Json(new { success = false, message = $"Thiếu chunk {i + 1}/{totalChunks}." });

                    var chunkSize = new FileInfo(chunkPath).Length;
                    totalMergedBytes += chunkSize;
                    if (totalMergedBytes > maxAllowedSize)
                    {
                        var limitMb = maxAllowedSize / 1024 / 1024;
                        return Json(new { success = false, message = $"File không được vượt quá {limitMb}MB." });
                    }

                    await using var chunkStream = new FileStream(chunkPath, FileMode.Open, FileAccess.Read);
                    await chunkStream.CopyToAsync(mergedStream);
                }
            }

            if (totalMergedBytes == 0)
                return Json(new { success = false, message = "File upload trống." });

            // Kiểm tra chữ ký đầu file (magic bytes) sau khi ghép xong để chặn đổi đuôi giả mạo
            await using (var sigStream = new FileStream(mergedPath, FileMode.Open, FileAccess.Read))
            {
                if (!MediaSecurityHelper.IsValidMediaSignature(sigStream, ext))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Nội dung file không khớp với định dạng khai báo (nghi ngờ đổi đuôi giả mạo)."
                    });
                }
            }

            _logger.LogInformation("[ChunkedUpload] Ghép xong {Chunks} chunk → {Path} ({Size}MB)",
                totalChunks, mergedPath, totalMergedBytes / 1024 / 1024);

            // Xử lý file hoàn chỉnh qua AudioService
            string audioUrl;
            await using (var stream = new FileStream(mergedPath, FileMode.Open, FileAccess.Read))
            {
                // Tạo IFormFile tạm từ stream đã ghép
                var tempFormFile = new FormFile(stream, 0, totalMergedBytes, "audioFile", $"upload{ext}");
                audioUrl = await _audioService.ProcessAndSaveAsync(tempFormFile);
            }

            // Lưu vào DB
            var soundTest = new SoundTest
            {
                SpecId   = specId,
                MicUsed  = micUsed?.Trim(),
                AudioUrl = audioUrl,
                CreatedAt = DateTime.UtcNow
            };
            _db.SoundTests.Add(soundTest);
            await _db.SaveChangesAsync();

            _logger.LogInformation("[ChunkedUpload] Upload hoàn tất → {Url}", audioUrl);

            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("Details", "Spec", new { id = specId })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChunkedUpload] Lỗi xử lý upload {UploadId}", uploadId);
            return Json(new { success = false, message = $"Lỗi xử lý file: {ex.Message}" });
        }
        finally
        {
            // Dọn dẹp: xóa thư mục chunk tạm
            try { Directory.Delete(sessionDir, recursive: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Không thể xóa temp chunks: {Dir}", sessionDir); }
            try { if (System.IO.File.Exists(mergedPath)) System.IO.File.Delete(mergedPath); }
            catch { }
        }
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

        // Xóa an toàn file âm thanh cục bộ (chỉ trong wwwroot/uploads/soundtests)
        MediaSecurityHelper.TryDeleteSafeLocalAudio(_env.WebRootPath, soundTest.AudioUrl);

        var specId = soundTest.SpecId;

        _db.SoundTests.Remove(soundTest);
        await _db.SaveChangesAsync();

        TempData["Success"] = "toast.soundtest.delete.success";
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

    /// <summary>
    /// Sửa nội dung bình luận — chỉ chủ comment.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment([FromBody] EditCommentRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Json(new { success = false, message = "Chưa đăng nhập." });

        if (string.IsNullOrWhiteSpace(req.Content))
            return Json(new { success = false, message = "Nội dung không được để trống." });

        var comment = await _db.SoundTestComments.FindAsync(req.CommentId);
        if (comment == null)
            return Json(new { success = false, message = "Không tìm thấy bình luận." });

        if (comment.UserId != userId)
            return Json(new { success = false, message = "Bạn không có quyền sửa." });

        comment.Content = req.Content.Trim();
        await _db.SaveChangesAsync();

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
                username = User.FindFirstValue(ClaimTypes.Name) ?? "User"
            }
        });
    }
}