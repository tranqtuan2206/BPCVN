using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace BPCVN.Services;

/// <summary>
/// Xử lý upload file âm thanh/video cho tính năng SoundTest.
///
/// Chiến lược lưu trữ:
///   - Audio (.mp3, .wav, .flac, .ogg, .m4a, .aac, .aiff) → Lưu Local
///   - Video (.mp4, .mov, .webm) → FFmpeg tách âm → Upload .mp3 lên Cloudinary
///     (FFmpeg tự tải về wwwroot/FFmpeg/ nếu chưa có)
/// </summary>
public class AudioService
{
    private static readonly HashSet<string> VideoExtensions =
        [".mp4", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".mxf"];

    private const string UploadSubPath = "uploads/soundtests";
    private const string TempSubPath = "temp";
    private const string FFmpegSubPath = "FFmpeg";
    private const string CloudinaryFolder = "BPCVN_Soundtests";

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AudioService> _logger;
    private readonly Cloudinary _cloudinary;
    private readonly string _ffmpegDir;

    public AudioService(IWebHostEnvironment env, ILogger<AudioService> logger, IConfiguration config)
    {
        _env    = env;
        _logger = logger;

        // FFmpeg lưu vào wwwroot/FFmpeg/ — thư mục ghi được trên IIS shared hosting
        _ffmpegDir = Path.Combine(_env.WebRootPath, FFmpegSubPath);
        FFmpeg.SetExecutablesPath(_ffmpegDir);

        var cloudName = config["CloudinarySettings:CloudName"];
        var apiKey    = config["CloudinarySettings:ApiKey"];
        var apiSecret = config["CloudinarySettings:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) || cloudName.StartsWith("YOUR_"))
            throw new InvalidOperationException("CloudinarySettings:CloudName chưa cấu hình.");
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("YOUR_"))
            throw new InvalidOperationException("CloudinarySettings:ApiKey chưa cấu hình.");
        if (string.IsNullOrWhiteSpace(apiSecret) || apiSecret.StartsWith("YOUR_"))
            throw new InvalidOperationException("CloudinarySettings:ApiSecret chưa cấu hình.");

        var account   = new Account(cloudName, apiKey, apiSecret);
        _cloudinary   = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<string> ProcessAndSaveAsync(IFormFile file)
    {
        var tempDir = Path.Combine(_env.WebRootPath, TempSubPath);
        Directory.CreateDirectory(tempDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var tempFileName = $"{Guid.NewGuid()}{ext}";
        var tempFilePath = Path.Combine(tempDir, tempFileName);

        try
        {
            await using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            _logger.LogInformation("[AudioService] Đã lưu file tạm: {Path}", tempFilePath);

            if (VideoExtensions.Contains(ext))
            {
                // Video: đảm bảo FFmpeg có sẵn → tách âm → upload .mp3 lên Cloudinary
                var ffmpegReady = await EnsureFFmpegAvailableAsync();
                if (!ffmpegReady)
                    throw new InvalidOperationException(
                        "Không thể xử lý video: FFmpeg chưa sẵn sàng. " +
                        "Vui lòng upload file âm thanh (.mp3, .wav, .m4a) thay vì video.");
                return await HandleVideoAsync(tempFilePath);
            }
            else
            {
                // Audio: lưu thẳng local
                return await HandleAudioAsync(tempFilePath, ext);
            }
        }
        catch
        {
            DeleteIfExists(tempFilePath);
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FFmpeg — Tự tải về nếu chưa có
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kiểm tra FFmpeg đã có chưa. Nếu chưa → tự tải về từ internet.
    /// Trả về true nếu FFmpeg sẵn sàng, false nếu tải thất bại.
    /// </summary>
    private async Task<bool> EnsureFFmpegAvailableAsync()
    {
        // 1. Kiểm tra FFmpeg đi kèm (được copy vào thư mục build/publish)
        var bundledDir = Path.Combine(AppContext.BaseDirectory, FFmpegSubPath);
        if (File.Exists(Path.Combine(bundledDir, "ffmpeg.exe")) || File.Exists(Path.Combine(bundledDir, "ffmpeg")))
        {
            _logger.LogInformation("[AudioService] Tìm thấy FFmpeg đi kèm tại: {Path}", bundledDir);
            FFmpeg.SetExecutablesPath(bundledDir);
            return true;
        }

        // 2. Kiểm tra FFmpeg ở thư mục gốc của project (lúc chạy debug local hoặc up lên share host)
        var contentDir = Path.Combine(_env.ContentRootPath, FFmpegSubPath);
        
        // Hỗ trợ trường hợp user đổi tên ffmpeg.exe thành custom.exe để lách luật SmarterASP
        if (File.Exists(Path.Combine(contentDir, "custom.exe")))
        {
            _logger.LogInformation("[AudioService] Tìm thấy custom.exe tại thư mục gốc: {Path}", contentDir);
            FFmpeg.SetExecutablesPath(contentDir, "custom", "ffprobe");
            return true;
        }
        
        if (File.Exists(Path.Combine(contentDir, "ffmpeg.exe")) || File.Exists(Path.Combine(contentDir, "ffmpeg")))
        {
            _logger.LogInformation("[AudioService] Tìm thấy FFmpeg tại thư mục gốc: {Path}", contentDir);
            FFmpeg.SetExecutablesPath(contentDir);
            return true;
        }

        // 3. Check FFmpeg đã có trong wwwroot/FFmpeg/ chưa (khi host trên IIS/shared host tải về)
        var ffmpegExe = Path.Combine(_ffmpegDir, "ffmpeg.exe");
        if (File.Exists(ffmpegExe) || File.Exists(Path.Combine(_ffmpegDir, "ffmpeg"))) 
        {
            FFmpeg.SetExecutablesPath(_ffmpegDir);
            return true;
        }

        // 4. Check FFmpeg trên system PATH (đã cài sẵn trên máy)
        var systemFfmpeg = FindFFmpegOnPath();
        if (systemFfmpeg != null)
        {
            var systemDir = Path.GetDirectoryName(systemFfmpeg)!;
            _logger.LogInformation("[AudioService] Tìm thấy FFmpeg trên system PATH: {Path}", systemDir);
            FFmpeg.SetExecutablesPath(systemDir);
            return true;
        }

        // 3. Tự tải FFmpeg từ internet về wwwroot/FFmpeg/
        _logger.LogWarning("[AudioService] FFmpeg chưa có, đang tải về {Path}...", _ffmpegDir);
        try
        {
            Directory.CreateDirectory(_ffmpegDir);
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, _ffmpegDir);
            if (File.Exists(ffmpegExe))
            {
                _logger.LogInformation("[AudioService] FFmpeg tải về thành công.");
                return true;
            }
            _logger.LogError("[AudioService] FFmpeg tải về nhưng file không tồn tại.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AudioService] Không thể tải FFmpeg: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Tìm ffmpeg trên system PATH. Trả về đường dẫn đầy đủ nếu tìm thấy, null nếu không.
    /// </summary>
    private static string? FindFFmpegOnPath()
    {
        var envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = envPath.Split(Path.PathSeparator);

        foreach (var dir in paths)
        {
            var exe = Path.Combine(dir, "ffmpeg.exe");
            if (File.Exists(exe)) return exe;

            // Linux/Mac: không có .exe
            var exeNoExt = Path.Combine(dir, "ffmpeg");
            if (File.Exists(exeNoExt)) return exeNoExt;
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // VIDEO — FFmpeg tách âm → Upload .mp3 lên Cloudinary
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<string> HandleVideoAsync(string videoTempPath)
    {
        var mp3TempPath = Path.Combine(
            Path.GetDirectoryName(videoTempPath)!,
            $"{Guid.NewGuid()}.mp3"
        );

        try
        {
            // Bước A: FFmpeg tách âm từ video → file .mp3 tạm
            var inputSize = new FileInfo(videoTempPath).Length;
            _logger.LogInformation("[AudioService] Bắt đầu xử lý video: {Path} ({Size}MB)",
                videoTempPath, inputSize / 1024 / 1024);

            await ExtractAudioToPathAsync(videoTempPath, mp3TempPath);

            // Kiểm tra file .mp3 đã được tạo chưa
            if (!File.Exists(mp3TempPath))
                throw new InvalidOperationException("FFmpeg không tạo được file .mp3 đầu ra.");

            var outputSize = new FileInfo(mp3TempPath).Length;
            _logger.LogInformation("[AudioService] FFmpeg tách âm thành công → {Path} ({Size}MB)",
                mp3TempPath, outputSize / 1024 / 1024);

            // Bước B: Upload file .mp3 lên Cloudinary (KHÔNG BAO GIỜ upload video)
            var audioUrl = await UploadToCloudinaryAsync(mp3TempPath);

            _logger.LogInformation("[AudioService] Video xử lý xong → Cloudinary URL: {Url}", audioUrl);
            return audioUrl;
        }
        finally
        {
            DeleteIfExists(videoTempPath);
            DeleteIfExists(mp3TempPath);
        }
    }

    /// <summary>
    /// FFmpeg tách âm thanh từ video, xuất ra .mp3.
    /// </summary>
    private async Task ExtractAudioToPathAsync(string videoPath, string outputMp3Path)
    {
        var conversion = FFmpeg.Conversions.New()
            .AddParameter($"-i \"{videoPath}\"")
            .AddParameter("-vn")           // bỏ qua stream video
            .AddParameter("-acodec mp3")   // encode thành mp3
            .AddParameter("-q:a 2")        // chất lượng VBR ~190kbps
            .SetOutput(outputMp3Path);

        _logger.LogInformation("[AudioService] FFmpeg tách âm: {Input} → {Output}", videoPath, outputMp3Path);
        await conversion.Start();
        _logger.LogInformation("[AudioService] FFmpeg hoàn tất tách âm.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AUDIO — Lưu local (convert sang .mp3 nếu cần)
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<string> HandleAudioAsync(string tempFilePath, string ext)
    {
        var uploadDir = Path.Combine(_env.WebRootPath, UploadSubPath);
        Directory.CreateDirectory(uploadDir);

        var outputFileName = $"{Guid.NewGuid()}.mp3";
        var outputPath     = Path.Combine(uploadDir, outputFileName);

        if (ext == ".mp3")
        {
            File.Move(tempFilePath, outputPath);
            _logger.LogInformation("[AudioService] .mp3 gốc → move vào uploads: {Name}", outputFileName);
        }
        else
        {
            // .wav / .flac / .ogg / .m4a / .aac / .aiff: convert sang .mp3 bằng FFmpeg
            var ffmpegReady = await EnsureFFmpegAvailableAsync();
            if (!ffmpegReady)
                throw new InvalidOperationException(
                    "Không thể convert audio: FFmpeg chưa sẵn sàng. " +
                    "Vui lòng upload file .mp3 thay vì .wav/.flac/.ogg/.m4a/.aac/.aiff.");

            await ConvertAudioToPathAsync(tempFilePath, outputPath);
            DeleteIfExists(tempFilePath);
            _logger.LogInformation("[AudioService] Audio convert xong → {Name}", outputFileName);
        }

        return $"/uploads/soundtests/{outputFileName}";
    }

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
    // Cloudinary Helper
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<string> UploadToCloudinaryAsync(string mp3FilePath)
    {
        // Safety check: CHỈ upload file .mp3, KHÔNG BAO GIỜ upload video
        var ext = Path.GetExtension(mp3FilePath).ToLowerInvariant();
        if (ext != ".mp3")
            throw new InvalidOperationException(
                $"[AudioService] An toàn: từ chối upload file {ext} lên Cloudinary. Chỉ chấp nhận .mp3.");

        var uploadParams = new VideoUploadParams
        {
            File = new FileDescription(mp3FilePath),
            Folder = CloudinaryFolder,
        };

        _logger.LogInformation("[AudioService] Đang upload lên Cloudinary: {Path}", mp3FilePath);
        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new Exception($"Cloudinary upload thất bại: {result.Error.Message}");

        _logger.LogInformation("[AudioService] Cloudinary upload thành công: {Url}", result.SecureUrl);
        return result.SecureUrl.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Utility
    // ══════════════════════════════════════════════════════════════════════════

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrEmpty(path))
            File.Delete(path);
    }
}
