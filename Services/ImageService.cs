using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace BPCVN.Services;

/// <summary>
/// Xử lý upload ảnh lên Cloudinary cho tính năng multi-image của Kit.
/// Upload vào folder "BPCVN_KitImages", ResourceType = Image.
/// </summary>
public class ImageService : IImageService
{
    private const string CloudinaryFolder = "BPCVN_KitImages";
    private const string TempSubPath = "temp";

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageService> _logger;
    private readonly Cloudinary _cloudinary;

    public ImageService(IWebHostEnvironment env, ILogger<ImageService> logger, IConfiguration config)
    {
        _env = env;
        _logger = logger;

        var cloudName = config["CloudinarySettings:CloudName"];
        var apiKey = config["CloudinarySettings:ApiKey"];
        var apiSecret = config["CloudinarySettings:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) || cloudName.StartsWith("YOUR_"))
            throw new InvalidOperationException(
                "CloudinarySettings:CloudName chưa cấu hình. Hãy đặt giá trị thật trong User Secrets.");
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("YOUR_"))
            throw new InvalidOperationException(
                "CloudinarySettings:ApiKey chưa cấu hình. Hãy đặt giá trị thật trong User Secrets.");
        if (string.IsNullOrWhiteSpace(apiSecret) || apiSecret.StartsWith("YOUR_"))
            throw new InvalidOperationException(
                "CloudinarySettings:ApiSecret chưa cấu hình. Hãy đặt giá trị thật trong User Secrets.");

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        // Tạo thư mục tạm nếu chưa có
        var tempDir = Path.Combine(_env.WebRootPath, TempSubPath);
        Directory.CreateDirectory(tempDir);

        // Tên file tạm dùng GUID để tránh trùng lặp
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var tempFileName = $"{Guid.NewGuid()}{ext}";
        var tempFilePath = Path.Combine(tempDir, tempFileName);

        try
        {
            // Lưu file tạm từ upload stream
            await using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Upload lên Cloudinary — ResourceType.Image để Cloudinary xử lý đúng
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(tempFilePath),
                Folder = CloudinaryFolder,
                // Tự tối ưu kích thước ảnh (max 2000px, quality auto)
                Transformation = new Transformation().Width(2000).Height(2000).Crop("limit").Quality("auto"),
            };

            _logger.LogInformation("[ImageService] Đang upload ảnh lên Cloudinary: {FileName}", file.FileName);

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                throw new Exception($"Cloudinary upload thất bại: {result.Error.Message}");

            _logger.LogInformation("[ImageService] Upload thành công: {Url}", result.SecureUrl);

            return result.SecureUrl.ToString();
        }
        finally
        {
            // Dọn file tạm — luôn thực hiện dù upload thành công hay thất bại
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }

    /// <summary>
    /// Upload nhiều file ảnh lên Cloudinary — duyệt từng file, upload tuần tự,
    /// trả về danh sách URL theo đúng thứ tự input.
    /// </summary>
    public async Task<List<string>> UploadImagesAsync(List<IFormFile> files)
    {
        var urls = new List<string>();
        foreach (var file in files)
        {
            var url = await UploadImageAsync(file);
            urls.Add(url);
        }
        return urls;
    }
}
