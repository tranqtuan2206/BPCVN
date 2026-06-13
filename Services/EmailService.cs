using System.Net;
using System.Net.Mail;

namespace BPCVN.Services;

/// <summary>
/// Service gửi email qua SMTP (Gmail).
/// Cấu hình đọc từ appsettings.json section "SmtpSettings".
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        // Đọc cấu hình SMTP từ appsettings.json
        var smtpHost = _config["SmtpSettings:Host"] ?? "smtp.gmail.com";
        var smtpPort = int.Parse(_config["SmtpSettings:Port"] ?? "587");
        var smtpUser = _config["SmtpSettings:Username"] ?? "";
        var smtpPass = _config["SmtpSettings:Password"] ?? ""; // ← Tự điền App Password của Gmail
        var fromEmail = _config["SmtpSettings:FromEmail"] ?? smtpUser;

        // Tạo mail message với nội dung HTML
        var mailMessage = new MailMessage
        {
            From = new MailAddress(fromEmail, "BPCVN"),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        // Cấu hình SMTP client
        using var smtpClient = new SmtpClient(smtpHost, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUser, smtpPass),
            EnableSsl = true // Gmail yêu cầu SSL/TLS
        };

        await smtpClient.SendMailAsync(mailMessage);
    }
}
