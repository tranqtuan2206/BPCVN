using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace BPCVN.SecurityTests;

/// <summary>
/// Tạo môi trường giả lập IWebHostEnvironment với thư mục tạm riêng để test an toàn.
/// </summary>
internal sealed class TempWebHostEnvironment : IWebHostEnvironment, IDisposable
{
    public TempWebHostEnvironment()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "bpcvn-sec-test-" + Guid.NewGuid().ToString("N"));
        WebRootPath = Path.Combine(ContentRootPath, "wwwroot");
        Directory.CreateDirectory(ContentRootPath);
        Directory.CreateDirectory(WebRootPath);

        ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
        WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
    }

    public string ApplicationName { get; set; } = "BPCVN.Web";
    public IFileProvider WebRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(ContentRootPath))
                Directory.Delete(ContentRootPath, recursive: true);
        }
        catch
        {
            // Bỏ qua lỗi dọn dẹp file tạm trên Windows nếu còn handle
        }
    }
}

internal static class TestFactory
{
    public static IConfiguration CreateCloudinaryConfig()
    {
        var values = new Dictionary<string, string?>
        {
            ["CloudinarySettings:CloudName"] = "test-cloud",
            ["CloudinarySettings:ApiKey"] = "1234567890",
            ["CloudinarySettings:ApiSecret"] = "test-secret"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    public static IFormFile CreateFormFile(byte[] bytes, string fileName)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BPCVN.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc chứa BPCVN.sln.");
    }
}
