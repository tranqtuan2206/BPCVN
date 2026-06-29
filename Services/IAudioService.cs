namespace BPCVN.Services;

/// <summary>
/// Interface định nghĩa các hành động xử lý file âm thanh cho SoundTest.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Nhận IFormFile, xử lý theo loại file, rồi lưu trữ:
    /// - Audio (.mp3, .wav, .flac, .ogg) → Lưu Local, trả về đường dẫn tương đối "/uploads/soundtests/xxx.mp3".
    /// - Video (.mp4, .mov) → FFmpeg tách âm → Upload Cloudinary → Xóa file local → trả về SecureUrl tuyệt đối.
    /// Ném exception nếu có lỗi trong quá trình xử lý.
    /// </summary>
    Task<string> ProcessAndSaveAsync(IFormFile file);
}
