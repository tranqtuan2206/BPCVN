using BPCVN.Infrastructure.Services;
using Xunit;

namespace BPCVN.SecurityTests;

/// <summary>
/// Kiểm thử chống Path Traversal khi giải quyết đường dẫn và xóa file âm thanh cục bộ.
/// </summary>
public class PathTraversalTests
{
    [Fact]
    public void ResolveSafeLocalPath_ShouldBlockTraversalAndAbsolutePaths()
    {
        using var env = new TempWebHostEnvironment();
        var baseDir = Path.Combine(env.WebRootPath, "uploads", "soundtests");
        Directory.CreateDirectory(baseDir);

        Assert.NotNull(MediaSecurityHelper.ResolveSafeLocalPath(baseDir, "valid-track.mp3"));
        Assert.Null(MediaSecurityHelper.ResolveSafeLocalPath(baseDir, "../../appsettings.json"));
        Assert.Null(MediaSecurityHelper.ResolveSafeLocalPath(baseDir, "..\\..\\web.config"));
        Assert.Null(MediaSecurityHelper.ResolveSafeLocalPath(baseDir, "../other/secret.txt"));
        Assert.Null(MediaSecurityHelper.ResolveSafeLocalPath(baseDir, ""));
    }

    [Fact]
    public void TryDeleteSafeLocalAudio_ShouldOnlyDeleteInsideUploadsSoundtests()
    {
        using var env = new TempWebHostEnvironment();
        var soundTestDir = Path.Combine(env.WebRootPath, "uploads", "soundtests");
        Directory.CreateDirectory(soundTestDir);

        var validFile = Path.Combine(soundTestDir, "sample.mp3");
        var sensitiveFile = Path.Combine(env.WebRootPath, "appsettings.json");
        var ffmpegDir = Path.Combine(env.WebRootPath, "FFmpeg");
        Directory.CreateDirectory(ffmpegDir);
        var ffmpegExe = Path.Combine(ffmpegDir, "custom.exe");

        File.WriteAllText(validFile, "audio");
        File.WriteAllText(sensitiveFile, "secret");
        File.WriteAllText(ffmpegExe, "bin");

        // Không được xóa các đường dẫn leo thư mục hoặc thư mục khác
        Assert.False(MediaSecurityHelper.TryDeleteSafeLocalAudio(env.WebRootPath, "/uploads/soundtests/../../appsettings.json"));
        Assert.False(MediaSecurityHelper.TryDeleteSafeLocalAudio(env.WebRootPath, "/FFmpeg/custom.exe"));
        Assert.False(MediaSecurityHelper.TryDeleteSafeLocalAudio(env.WebRootPath, "https://res.cloudinary.com/demo/video/upload/test.mp3"));
        Assert.True(File.Exists(sensitiveFile));
        Assert.True(File.Exists(ffmpegExe));

        // Chỉ cho phép xóa đúng file trong /uploads/soundtests/
        Assert.True(MediaSecurityHelper.TryDeleteSafeLocalAudio(env.WebRootPath, "/uploads/soundtests/sample.mp3"));
        Assert.False(File.Exists(validFile));
    }
}
