using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace BPCVN.Services;

/// <summary>
/// Xử lý upload ảnh lên Cloudinary cho tính năng multi-image của Kit.
/// Upload vào folder "BPCVN_KitImages", ResourceType = Image.
/// </summary>
public class ImageService
{
    private const string CloudinaryFolder = "BPCVN_KitImages";

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
        // Upload trực tiếp từ stream lên Cloudinary thay vì tạo file tạm trên ổ cứng
        await using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
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
