using System.Security.Cryptography;
using System.Text;

namespace BPCVN.Application.Services;

/// <summary>
/// Tiện ích sinh, băm và xác minh token đặt lại mật khẩu an toàn.
/// Token gốc chỉ gửi qua email; cơ sở dữ liệu chỉ lưu mã băm SHA-256.
/// </summary>
public static class PasswordResetTokenHelper
{
    /// <summary>
    /// Sinh token ngẫu nhiên 256-bit (32 byte) theo chuẩn Base64 URL-safe.
    /// </summary>
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Băm token bằng SHA-256 và trả về chuỗi hex chữ thường (64 ký tự).
    /// </summary>
    public static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Xác minh token người dùng gửi lên với mã băm và thời hạn lưu trong cơ sở dữ liệu
    /// bằng phép so sánh thời gian hằng số (chống timing attack).
    /// </summary>
    public static bool VerifyToken(
        string? rawToken,
        string? storedHash,
        DateTime? expiryUtc,
        DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        var currentUtc = nowUtc ?? DateTime.UtcNow;
        if (!expiryUtc.HasValue || expiryUtc.Value <= currentUtc)
            return false;

        var computedBytes = Encoding.UTF8.GetBytes(HashToken(rawToken.Trim()));
        var storedBytes = Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant());

        if (computedBytes.Length != storedBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
