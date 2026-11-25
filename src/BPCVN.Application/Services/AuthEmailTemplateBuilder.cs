using System.Net;

namespace BPCVN.Application.Services;

/// <summary>
/// Tạo mẫu email xác thực và đặt lại mật khẩu gọn gàng theo phong cách Neo-Brutalist,
/// tự động chọn đúng ngôn ngữ (Tiếng Việt / Tiếng Anh) và ẩn chuỗi token thô khỏi giao diện email.
/// </summary>
public static class AuthEmailTemplateBuilder
{
    /// <summary>
    /// Xác định ngôn ngữ gửi email theo thứ tự ưu tiên:
    /// 1. Cookie bpcvn-lang (do người dùng chọn trên web)
    /// 2. Header Accept-Language của trình duyệt
    /// 3. Mặc định "vi"
    /// </summary>
    public static string ResolveEmailLanguage(string? cookieLang, string? acceptLanguageHeader)
    {
        if (!string.IsNullOrWhiteSpace(cookieLang))
        {
            var normalizedCookie = cookieLang.Trim().ToLowerInvariant();
            if (normalizedCookie == "en") return "en";
            if (normalizedCookie == "vi") return "vi";
        }

        if (!string.IsNullOrWhiteSpace(acceptLanguageHeader))
        {
            var firstLang = acceptLanguageHeader
                .Split(',', StringSplitOptions.RemoveEmptyEntries)[0]
                .Split(';', StringSplitOptions.RemoveEmptyEntries)[0]
                .Trim()
                .ToLowerInvariant();

            if (!string.IsNullOrEmpty(firstLang))
                return firstLang.StartsWith("vi") ? "vi" : "en";
        }

        return "vi";
    }

    /// <summary>
    /// Tạo tiêu đề và nội dung HTML cho email kích hoạt tài khoản.
    /// </summary>
    public static (string Subject, string Body) BuildVerifyAccountEmail(string username, string verifyLink, string lang)
    {
        bool isEn = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

        return isEn
            ? BuildActionEmail(
                subject: "BPCVN - Verify your account",
                badge: "ACCOUNT VERIFICATION",
                heading: "Welcome to BPCVN!",
                greeting: $"Hello <strong>{WebUtility.HtmlEncode(username)}</strong>,",
                intro: "Thanks for joining the Vietnamese Mechanical Keyboard Community. Click the button below to activate your account:",
                ctaLabel: "Activate Account",
                fallbackPrefix: "If the button above does not work,",
                fallbackLinkText: "click this direct link",
                extraNote: "",
                footerNote: "If you did not create this account, you can safely ignore this email.",
                actionUrl: verifyLink)
            : BuildActionEmail(
                subject: "BPCVN - Xác thực tài khoản",
                badge: "XÁC THỰC TÀI KHOẢN",
                heading: "Chào mừng bạn đến với BPCVN!",
                greeting: $"Xin chào <strong>{WebUtility.HtmlEncode(username)}</strong>,",
                intro: "Cảm ơn bạn đã đăng ký tham gia cộng đồng Bàn Phím Cơ Việt Nam. Vui lòng nhấn nút bên dưới để kích hoạt tài khoản:",
                ctaLabel: "Kích hoạt tài khoản",
                fallbackPrefix: "Nếu nút trên không hoạt động, hãy",
                fallbackLinkText: "nhấn trực tiếp vào đây",
                extraNote: "",
                footerNote: "Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.",
                actionUrl: verifyLink);
    }

    /// <summary>
    /// Tạo tiêu đề và nội dung HTML cho email đặt lại mật khẩu (hiệu lực 15 phút, dùng 1 lần).
    /// </summary>
    public static (string Subject, string Body) BuildPasswordResetEmail(string username, string resetLink, string lang)
    {
        bool isEn = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

        return isEn
            ? BuildActionEmail(
                subject: "BPCVN - Reset your password",
                badge: "PASSWORD RECOVERY",
                heading: "Password Reset Request",
                greeting: $"Hello <strong>{WebUtility.HtmlEncode(username)}</strong>,",
                intro: "We received a request to reset the password for your BPCVN account. Click the button below to set a new password:",
                ctaLabel: "Reset Password",
                fallbackPrefix: "If the button above does not work,",
                fallbackLinkText: "click this direct link",
                extraNote: "Note: This link is valid for <strong>15 minutes</strong> and can only be used once.",
                footerNote: "If you did not request a password reset, please ignore this email.",
                actionUrl: resetLink)
            : BuildActionEmail(
                subject: "BPCVN - Đặt lại mật khẩu",
                badge: "KHÔI PHỤC MẬT KHẨU",
                heading: "Yêu cầu đặt lại mật khẩu",
                greeting: $"Xin chào <strong>{WebUtility.HtmlEncode(username)}</strong>,",
                intro: "Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản BPCVN của bạn. Nhấn nút bên dưới để tạo mật khẩu mới:",
                ctaLabel: "Đặt lại mật khẩu",
                fallbackPrefix: "Nếu nút trên không hoạt động, hãy",
                fallbackLinkText: "nhấn trực tiếp vào đây",
                extraNote: "Lưu ý: Liên kết này chỉ có hiệu lực trong <strong>15 phút</strong> và chỉ dùng được một lần.",
                footerNote: "Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email này.",
                actionUrl: resetLink);
    }

    private static (string Subject, string Body) BuildActionEmail(
        string subject,
        string badge,
        string heading,
        string greeting,
        string intro,
        string ctaLabel,
        string fallbackPrefix,
        string fallbackLinkText,
        string extraNote,
        string footerNote,
        string actionUrl)
    {
        var safeUrl = WebUtility.HtmlEncode(actionUrl);
        var noteHtml = string.IsNullOrEmpty(extraNote)
            ? ""
            : $"<p style=\"margin:14px 0 0 0;padding:10px 12px;background:#f4f1e4;border:2px dashed #1a1a1a;font-size:13px;color:#1a1a1a;\">{extraNote}</p>";

        // Khung email Neo-Brutalist gọn nhẹ, không hiển thị URL thô chứa token ra nội dung văn bản
        var body = $@"
<div style=""font-family:'Inter',Arial,sans-serif;max-width:520px;margin:0 auto;background:#FFFDF0;border:3px solid #1a1a1a;color:#1a1a1a;"">
  <div style=""background:#FF66C4;border-bottom:3px solid #1a1a1a;padding:16px 22px;"">
    <span style=""display:inline-block;background:#FFFDF0;color:#1a1a1a;border:2px solid #1a1a1a;padding:3px 9px;font-size:11px;font-weight:800;letter-spacing:0.06em;"">{badge}</span>
    <h2 style=""margin:10px 0 0 0;font-size:20px;font-weight:800;color:#1a1a1a;"">{heading}</h2>
  </div>
  <div style=""padding:22px;font-size:15px;line-height:1.55;color:#1a1a1a;"">
    <p style=""margin:0 0 10px 0;"">{greeting}</p>
    <p style=""margin:0 0 18px 0;"">{intro}</p>
    <p style=""margin:0 0 18px 0;"">
      <a href=""{safeUrl}"" style=""display:inline-block;padding:12px 22px;background:#1a1a1a;color:#FFFDF0;text-decoration:none;font-weight:800;border:3px solid #1a1a1a;"">{ctaLabel}</a>
    </p>
    <p style=""margin:0;font-size:13px;color:#444;"">
      {fallbackPrefix} <a href=""{safeUrl}"" style=""color:#1a1a1a;font-weight:700;text-decoration:underline;"">{fallbackLinkText}</a>.
    </p>
    {noteHtml}
    <hr style=""border:0;border-top:2px solid #1a1a1a;margin:18px 0 12px 0;"" />
    <p style=""margin:0;font-size:12px;color:#5f5f5d;"">{footerNote}</p>
  </div>
</div>";

        return (subject, body);
    }
}
