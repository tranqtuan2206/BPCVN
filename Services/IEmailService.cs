namespace BPCVN.Services;

/// <summary>
/// Interface dịch vụ gửi email — dùng cho xác thực tài khoản.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Gửi email tới địa chỉ <paramref name="toEmail"/>.
    /// </summary>
    /// <param name="toEmail">Địa chỉ email người nhận.</param>
    /// <param name="subject">Tiêu đề email.</param>
    /// <param name="body">Nội dung email (HTML).</param>
    Task SendEmailAsync(string toEmail, string subject, string body);
}
