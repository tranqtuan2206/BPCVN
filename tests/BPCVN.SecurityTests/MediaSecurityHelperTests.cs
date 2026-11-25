using System.Text;
using BPCVN.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BPCVN.SecurityTests;

/// <summary>
/// Kiểm thử kiểm tra định dạng file, magic bytes (chống đổi tên đuôi) cho AudioService và ImageService.
/// </summary>
public class MediaSecurityHelperTests
{
    [Theory]
    [InlineData(".mp3", new byte[] { 0x49, 0x44, 0x33, 0x03, 0x00, 0x00 })] // ID3v2
    [InlineData(".mp3", new byte[] { 0xFF, 0xFB, 0x90, 0x64 })]             // MPEG-1 Layer 3 frame sync
    [InlineData(".wav", new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 })] // RIFF...WAVE
    [InlineData(".flac", new byte[] { 0x66, 0x4C, 0x61, 0x43, 0x00, 0x00 })] // fLaC
    [InlineData(".ogg", new byte[] { 0x4F, 0x67, 0x67, 0x53, 0x00, 0x02 })]  // OggS
    [InlineData(".mp4", new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D })] // ftyp
    [InlineData(".mov", new byte[] { 0x00, 0x00, 0x00, 0x14, 0x6D, 0x6F, 0x6F, 0x76 })] // moov
    [InlineData(".webm", new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x9F })]       // EBML
    public void ValidMediaSignatures_ShouldPass(string ext, byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        Assert.True(MediaSecurityHelper.IsValidMediaSignature(ms, ext));
        Assert.Equal(0, ms.Position);
    }

    [Theory]
    [InlineData(".mp3", "<html><p>not-audio</p></html>")]
    [InlineData(".mp3", "MZ-header-binary-spoof")]
    [InlineData(".mp4", "<?php echo 'not-video'; ?>")]
    [InlineData(".wav", "PK\x03\x04fake-zip-archive")]
    [InlineData(".ogg", "%PDF-1.7\nfake")]
    [InlineData(".mp3", "day la file text doi duoi thanh mp3")]
    [InlineData(".flac", "RIFF\x00\x00\x00\x00WAVE")] // Sai định dạng (WAV đổi thành FLAC)
    public void SpoofedMediaExtensions_ShouldBeRejected(string ext, string rawContent)
    {
        var bytes = Encoding.UTF8.GetBytes(rawContent);
        using var ms = new MemoryStream(bytes);
        Assert.False(MediaSecurityHelper.IsValidMediaSignature(ms, ext));
    }

    [Theory]
    [InlineData(".jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 })]
    [InlineData(".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData(".gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })]
    [InlineData(".webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 })]
    public void ValidImageSignatures_ShouldPass(string ext, byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        Assert.True(MediaSecurityHelper.IsValidImageSignature(ms, ext));
    }

    [Theory]
    [InlineData(".png", "<svg xmlns='http://www.w3.org/2000/svg'></svg>")]
    [InlineData(".jpg", "<html><body>not-image</body></html>")]
    [InlineData(".webp", "MZ-fake-image")]
    [InlineData(".svg", "<svg xmlns='http://www.w3.org/2000/svg'></svg>")]
    [InlineData(".png", "not-a-png-file")]
    public void SpoofedOrDangerousImages_ShouldBeRejected(string ext, string rawContent)
    {
        var bytes = Encoding.UTF8.GetBytes(rawContent);
        using var ms = new MemoryStream(bytes);
        Assert.False(MediaSecurityHelper.IsValidImageSignature(ms, ext));
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".html")]
    [InlineData(".php")]
    [InlineData(".svg")]
    [InlineData(".mp3.exe")]
    [InlineData("../../evil.mp3")]
    public void DisallowedExtensions_ShouldBeRejected(string ext)
    {
        Assert.False(MediaSecurityHelper.IsAllowedMediaExtension(ext));
    }

    [Fact]
    public async Task AudioService_ShouldRejectRenamedExecutableAndSaveNothingToWwwroot()
    {
        using var env = new TempWebHostEnvironment();
        var service = new AudioService(env, NullLogger<AudioService>.Instance, TestFactory.CreateCloudinaryConfig());

        // Giả lập file có header PE ('M','Z') đổi đuôi thành .mp3
        var fakeMp3 = TestFactory.CreateFormFile(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 }, "fake-exec.mp3");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAndSaveAsync(fakeMp3));
        Assert.Contains("giả mạo", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Đảm bảo không tạo bất kỳ file nào trong wwwroot/uploads hoặc wwwroot/temp
        Assert.False(Directory.Exists(Path.Combine(env.WebRootPath, "uploads", "soundtests")));
        Assert.False(Directory.Exists(Path.Combine(env.WebRootPath, "temp")));
    }

    [Fact]
    public async Task ImageService_ShouldRejectSvgOrSpoofedPngBeforeCallingCloudinary()
    {
        using var env = new TempWebHostEnvironment();
        var service = new ImageService(env, NullLogger<ImageService>.Instance, TestFactory.CreateCloudinaryConfig());

        var svgFile = TestFactory.CreateFormFile(Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'></svg>"), "avatar.svg");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadImageAsync(svgFile));

        var spoofedPng = TestFactory.CreateFormFile(Encoding.UTF8.GetBytes("<html>not-a-png</html>"), "fake.png");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadImageAsync(spoofedPng));
    }
}
