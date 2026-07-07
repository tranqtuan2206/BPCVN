namespace BPCVN.Services;

/// <summary>
/// Dịch vụ upload ảnh lên Cloudinary cho tính năng multi-image.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Upload một file ảnh lên Cloudinary, trả về SecureUrl (HTTPS).
    /// </summary>
    Task<string> UploadImageAsync(IFormFile file);

    /// <summary>
    /// Upload nhiều file ảnh lên Cloudinary, trả về danh sách SecureUrl.
    /// </summary>
    Task<List<string>> UploadImagesAsync(List<IFormFile> files);
}
