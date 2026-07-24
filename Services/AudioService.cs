using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Xabe.FFmpeg;

namespace BPCVN.Services;

/// <summary>
/// Xử lý upload file âm thanh/video cho tính năng SoundTest.
///
/// Chiến lược lưu trữ:
///   - Audio (.mp3, .wav, .flac, .ogg) → Lưu Local (wwwroot/uploads/soundtests).
///   - Video (.mp4, .mov)              → FFmpeg tách âm → .mp3 tạm → Upload Cloudinary
///                                        → Xóa sạch file local → Trả về SecureUrl.
/// </summary>
public class AudioService : IAudioService
{
    // Các đuôi file phân loại là video → cần FFmpeg + Cloudinary
    private static readonly HashSet<string> VideoExtensions = [".mp4", ".mov", ".avi", ".mkv"];

    // Thư mục lưu trữ local cuối cùng (chỉ dùng cho Audio)
    private const string UploadSubPath = "uploads/soundtests";

    // Thư mục tạm — dùng cho mọi loại file khi nhận từ user
    private const string TempSubPath = "temp";

    // Tên folder trên Cloudinary
    private const string CloudinaryFolder = "BPCVN_Soundtests";

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AudioService> _logger;
    private readonly Cloudinary _cloudinary;

    public AudioService(IWebHostEnvironment env, ILogger<AudioService> logger, IConfiguration config)
    {
        _env    = env;
        _logger = logger;

        // ── Khởi tạo FFmpeg ──────────────────────────────────────────────────
        // ffmpeg.exe phải nằm trong thư mục FFmpeg/ của project
        // và được cấu hình CopyToOutputDirectory trong .csproj
        var ffmpegDir = Path.Combine(AppContext.BaseDirectory, "FFmpeg");
        FFmpeg.SetExecutablesPath(ffmpegDir);

        // ── Khởi tạo Cloudinary từ appsettings ───────────────────────────────
        var cloudName = config["CloudinarySettings:CloudName"];
        var apiKey    = config["CloudinarySettings:ApiKey"];
        var apiSecret = config["CloudinarySettings:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) || cloudName.StartsWith("YOUR_"))
            throw new InvalidOperationException(
                "CloudinarySettings:CloudName chưa cấu hình. " +
                "Hãy đặt giá trị thật trong User Secrets (dotnet user-secrets set) hoặc appsettings.Development.json.");
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("YOUR_"))
            throw new InvalidOperationException(
                "CloudinarySettings:ApiKey chưa cấu hình. " +
                "Hãy đặt giá trị thật trong User Secrets (dotnet user-secrets set) hoặc appsettings.Development.json.");
        if (string.IsNullOrWhiteSpace(apiSecret) || apiSecret.StartsWith("YOUR_"))
            throw new InvalidOperationException(
                "CloudinarySettings:ApiSecret chưa cấu hình. " +
                "Hãy đặt giá trị thật trong User Secrets (dotnet user-secrets set) hoặc appsettings.Development.json.");

        var account   = new Account(cloudName, apiKey, apiSecret);
        _cloudinary   = new Cloudinary(account);
        _cloudinary.Api.Secure = true; // Bắt buộc trả về HTTPS (SecureUrl)
    }

    /// <inheritdoc />
    public async Task<string> ProcessAndSaveAsync(IFormFile file)
    {
        // Chuẩn bị thư mục tạm
        var tempDir = Path.Combine(_env.WebRootPath, TempSubPath);
        Directory.CreateDirectory(tempDir);

        // Lấy đuôi file gốc (lowercase) để phân loại
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Tên file tạm dùng GUID để tránh xung đột khi nhiều request đồng thời
        var tempFileName = $"{Guid.NewGuid()}{ext}";
        var tempFilePath = Path.Combine(tempDir, tempFileName);

        try
        {
            // ── BƯỚC 1: Lưu file user upload vào thư mục tạm ─────────────────
            await using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            _logger.LogInformation("[AudioService] Đã lưu file tạm: {Path}", tempFilePath);

            // ── BƯỚC 2: Phân nhánh xử lý theo loại file ──────────────────────
            if (VideoExtensions.Contains(ext))
            {
                // Luồng Video: FFmpeg → .mp3 tạm → Cloudinary → Xóa local
                return await HandleVideoAsync(tempFilePath);
            }
            else
            {
                // Luồng Audio: lưu thẳng local
                return await HandleAudioAsync(tempFilePath, ext);
            }
        }
        catch
        {
            // Nếu có lỗi trong xử lý: đảm bảo xóa file tạm đầu vào
            DeleteIfExists(tempFilePath);
            throw; // Re-throw để Controller bắt và báo lỗi cho user
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Luồng VIDEO: FFmpeg tách âm → Upload Cloudinary → Dọn local
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tách âm từ video, upload .mp3 lên Cloudinary, xóa sạch file local.
    /// Trả về SecureUrl tuyệt đối (https://res.cloudinary.com/...).
    /// </summary>
    private async Task<string> HandleVideoAsync(string videoTempPath)
    {
        // Đường dẫn file .mp3 tạm (cùng thư mục temp, tên GUID mới)
        var mp3TempPath = Path.Combine(
            Path.GetDirectoryName(videoTempPath)!,
            $"{Guid.NewGuid()}.mp3"
        );

        try
        {
            // ── Bước A: FFmpeg tách âm từ video → file .mp3 tạm ─────────────
            await ExtractAudioToPathAsync(videoTempPath, mp3TempPath);

            // ── Bước B: Upload file .mp3 lên Cloudinary ───────────────────────
            var secureUrl = await UploadToCloudinaryAsync(mp3TempPath);

            _logger.LogInformation("[AudioService] Video xử lý xong → Cloudinary URL: {Url}", secureUrl);
            return secureUrl;
        }
        finally
        {
            // ── Bước C: Dọn rác triệt để — xóa cả video gốc lẫn .mp3 tạm ────
            DeleteIfExists(videoTempPath);
            DeleteIfExists(mp3TempPath);
            _logger.LogInformation("[AudioService] Đã dọn rác file local: video + mp3 tạm.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Luồng AUDIO: Lưu local (convert sang .mp3 nếu cần)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Xử lý file audio: move hoặc convert sang .mp3, lưu vào wwwroot/uploads/soundtests.
    /// Trả về đường dẫn tương đối "/uploads/soundtests/xxx.mp3".
    /// </summary>
    private async Task<string> HandleAudioAsync(string tempFilePath, string ext)
    {
        var uploadDir    = Path.Combine(_env.WebRootPath, UploadSubPath);
        Directory.CreateDirectory(uploadDir);

        var outputFileName = $"{Guid.NewGuid()}.mp3";
        var outputPath     = Path.Combine(uploadDir, outputFileName);

        if (ext == ".mp3")
        {
            // .mp3 gốc: move thẳng, không cần re-encode
            File.Move(tempFilePath, outputPath);
            _logger.LogInformation("[AudioService] .mp3 gốc → move vào uploads: {Name}", outputFileName);
        }
        else
        {
            // .wav / .flac / .ogg: convert sang .mp3 bằng FFmpeg
            await ConvertAudioToPathAsync(tempFilePath, outputPath);
            DeleteIfExists(tempFilePath); // Xóa file tạm sau khi convert xong
            _logger.LogInformation("[AudioService] Audio convert xong → {Name}", outputFileName);
        }

        // Trả về đường dẫn tương đối để lưu DB (giống cách cũ)
        return $"/uploads/soundtests/{outputFileName}";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — FFmpeg Helpers
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dùng FFmpeg tách âm thanh từ file video, xuất ra .mp3 tại đường dẫn chỉ định.
    /// </summary>
    private async Task ExtractAudioToPathAsync(string videoPath, string outputMp3Path)
    {
        // Dùng chung path đã set ở constructor (AppContext.BaseDirectory/FFmpeg)
        // Tránh trường hợp Directory.GetCurrentDirectory() trả sai path trên production

        // -vn        : bỏ qua stream video
        // -acodec mp3: encode âm thanh thành mp3
        // -q:a 2     : chất lượng VBR ~190kbps (0=tốt nhất, 9=kém nhất)
        var conversion = FFmpeg.Conversions.New()
            .AddParameter($"-i \"{videoPath}\"")
            .AddParameter("-vn")
            .AddParameter("-acodec mp3")
            .AddParameter("-q:a 2")
            .SetOutput(outputMp3Path);

        _logger.LogInformation("[AudioService] FFmpeg bắt đầu tách âm: {Input} → {Output}",
            videoPath, outputMp3Path);
        await conversion.Start();
        _logger.LogInformation("[AudioService] FFmpeg hoàn tất tách âm.");
    }

    /// <summary>
    /// Dùng FFmpeg convert file audio sang .mp3 tại đường dẫn chỉ định.
    /// </summary>
    private async Task ConvertAudioToPathAsync(string inputPath, string outputMp3Path)
    {
        var conversion = FFmpeg.Conversions.New()
            .AddParameter($"-i \"{inputPath}\"")
            .AddParameter("-acodec mp3")
            .AddParameter("-q:a 2")
            .SetOutput(outputMp3Path);

        _logger.LogInformation("[AudioService] FFmpeg convert audio: {Input} → mp3", inputPath);
        await conversion.Start();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Cloudinary Helper
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Upload file .mp3 lên Cloudinary trong folder "BPCVN_Soundtests".
    /// ResourceType = "video" vì Cloudinary phân loại audio vào nhóm này.
    /// Trả về SecureUrl (https://...) để lưu thẳng vào Database.
    /// </summary>
    private async Task<string> UploadToCloudinaryAsync(string mp3FilePath)
    {
        var uploadParams = new VideoUploadParams
        {
            // Đường dẫn file local cần upload
            File = new FileDescription(mp3FilePath),

            // Đặt vào folder riêng trên Cloudinary để dễ quản lý
            Folder = CloudinaryFolder,

            // "auto" để Cloudinary tự phát hiện là audio
            // Dùng RawUploadParams thay VideoUploadParams để tương thích tốt hơn với .mp3
        };

        _logger.LogInformation("[AudioService] Đang upload lên Cloudinary folder: {Folder}", CloudinaryFolder);

        var result = await _cloudinary.UploadAsync(uploadParams);

        // Kiểm tra lỗi từ Cloudinary
        if (result.Error != null)
        {
            throw new Exception($"Cloudinary upload thất bại: {result.Error.Message}");
        }

        _logger.LogInformation("[AudioService] Cloudinary upload thành công: {Url}", result.SecureUrl);

        // Trả về URL tuyệt đối HTTPS (SecureUrl)
        return result.SecureUrl.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Utility
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Xóa file nếu tồn tại (an toàn, không ném exception nếu file đã bị xóa).
    /// </summary>
    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            File.Delete(path);
    }
}
