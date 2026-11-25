using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using BPCVN.Application.Services;
using BPCVN.Application.ViewModels;
using Xunit;

namespace BPCVN.SecurityTests;

/// <summary>
/// Kiểm thử chức năng Quên mật khẩu / Đặt lại mật khẩu (token, hạn dùng, validation, song ngữ).
/// </summary>
public class PasswordResetTests
{
    [Fact]
    public void GenerateToken_ShouldBeUrlSafeUniqueAndStrong()
    {
        var t1 = PasswordResetTokenHelper.GenerateToken();
        var t2 = PasswordResetTokenHelper.GenerateToken();

        Assert.NotEqual(t1, t2);
        Assert.True(t1.Length >= 40);
        Assert.DoesNotContain("+", t1);
        Assert.DoesNotContain("/", t1);
        Assert.DoesNotContain("=", t1);
    }

    [Fact]
    public void HashToken_ShouldProduceConsistent64CharLowerHexDifferentFromRaw()
    {
        var raw = PasswordResetTokenHelper.GenerateToken();
        var h1 = PasswordResetTokenHelper.HashToken(raw);
        var h2 = PasswordResetTokenHelper.HashToken(raw);

        Assert.Equal(h1, h2);
        Assert.NotEqual(raw, h1);
        Assert.Equal(64, h1.Length);
        Assert.Matches("^[0-9a-f]{64}$", h1);
    }

    [Fact]
    public void VerifyToken_ShouldValidateCorrectlyAndRejectExpiredOrWrongTokens()
    {
        var now = new DateTime(2026, 11, 25, 7, 0, 0, DateTimeKind.Utc);
        var raw = PasswordResetTokenHelper.GenerateToken();
        var hash = PasswordResetTokenHelper.HashToken(raw);
        var validExpiry = now.AddMinutes(15);
        var expiredExpiry = now.AddSeconds(-1);

        Assert.True(PasswordResetTokenHelper.VerifyToken(raw, hash, validExpiry, now));
        Assert.False(PasswordResetTokenHelper.VerifyToken(raw + "x", hash, validExpiry, now));
        Assert.False(PasswordResetTokenHelper.VerifyToken(raw, hash, expiredExpiry, now));
        Assert.False(PasswordResetTokenHelper.VerifyToken(raw, hash, now, now)); // Đúng thời điểm hết hạn
        Assert.False(PasswordResetTokenHelper.VerifyToken("", hash, validExpiry, now));
        Assert.False(PasswordResetTokenHelper.VerifyToken(raw, null, validExpiry, now));
    }

    [Fact]
    public void ViewModels_ShouldEnforceEmailAndPasswordValidation()
    {
        var invalidForgot = new ForgotPasswordViewModel { Email = "not-an-email" };
        var forgotErrors = ValidateModel(invalidForgot);
        Assert.NotEmpty(forgotErrors);

        var mismatchReset = new ResetPasswordViewModel
        {
            Email = "user@example.com",
            Token = "abc",
            NewPassword = "Password123",
            ConfirmNewPassword = "DifferentPassword"
        };
        var mismatchErrors = ValidateModel(mismatchReset);
        Assert.Contains(mismatchErrors, e => e.ErrorMessage == "Mật khẩu xác nhận không khớp");

        var shortReset = new ResetPasswordViewModel
        {
            Email = "user@example.com",
            Token = "abc",
            NewPassword = "123",
            ConfirmNewPassword = "123"
        };
        var shortErrors = ValidateModel(shortReset);
        Assert.Contains(shortErrors, e => e.ErrorMessage == "Mật khẩu mới ít nhất 6 ký tự");
    }

    [Fact]
    public void Translations_ShouldContainBalancedEnAndViKeysForPasswordReset()
    {
        var root = TestFactory.FindRepoRoot();
        var jsPath = Path.Combine(root, "src/BPCVN.Web/wwwroot/js/translations.js");
        var content = File.ReadAllText(jsPath);

        string[] requiredKeys =
        [
            "login.forgotPassword",
            "auth.forgot.badge",
            "auth.forgot.title",
            "auth.forgot.subtitle",
            "auth.forgot.emailLabel",
            "auth.forgot.emailPlaceholder",
            "auth.forgot.expiryNote",
            "auth.forgot.submit",
            "auth.forgot.backToLogin",
            "auth.forgot.sentBadge",
            "auth.forgot.sentTitle",
            "auth.forgot.sentMessage",
            "auth.forgot.sentSpamHint",
            "auth.forgot.sendAnother",
            "auth.reset.badge",
            "auth.reset.title",
            "auth.reset.subtitle",
            "auth.reset.accountLabel",
            "auth.reset.newLabel",
            "auth.reset.newPlaceholder",
            "auth.reset.confirmLabel",
            "auth.reset.confirmPlaceholder",
            "auth.reset.hint",
            "auth.reset.submit",
            "auth.reset.togglePassword",
            "toast.auth.forgot.sent",
            "toast.auth.reset.invalid",
            "toast.auth.reset.success"
        ];

        foreach (var key in requiredKeys)
        {
            // Mỗi key phải xuất hiện đúng 2 lần (1 lần trong en, 1 lần trong vi)
            var count = Regex.Matches(content, Regex.Escape($"'{key}':")).Count;
            Assert.Equal(2, count);
        }
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }
}
