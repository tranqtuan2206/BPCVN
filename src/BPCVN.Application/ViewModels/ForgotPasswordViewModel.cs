using System.ComponentModel.DataAnnotations;

namespace BPCVN.Application.ViewModels;

/// <summary>
/// ViewModel cho trang yêu cầu đặt lại mật khẩu (nhập email tài khoản).
/// </summary>
public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
