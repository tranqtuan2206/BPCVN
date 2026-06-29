using System.ComponentModel.DataAnnotations;

namespace BPCVN.Models.Entities;

public class User
{
    [Key]
    public Guid UserId { get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Username là bắt buộc.")]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Luôn lưu hash (BCrypt), KHÔNG lưu plaintext.</summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Phân quyền: "User" (mặc định) hoặc "Admin"</summary>
    [StringLength(20)]
    public string Role { get; set; } = "User";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Xác thực Email ───────────────────────────────────────────────────
    /// <summary>Trạng thái xác thực email — false = chưa kích hoạt.</summary>
    public bool IsEmailConfirmed { get; set; } = false;

    /// <summary>Token dùng để xác thực email (Guid), null sau khi đã kích hoạt.</summary>
    public string? VerificationToken { get; set; }

    // Navigation
    public ICollection<Spec> Specs { get; set; } = new List<Spec>();

    /// <summary>Danh sách lượt Like SoundTest của user</summary>
    public ICollection<SoundTestLike> SoundTestLikes { get; set; } = new List<SoundTestLike>();

    /// <summary>Danh sách bình luận SoundTest của user</summary>
    public ICollection<SoundTestComment> SoundTestComments { get; set; } = new List<SoundTestComment>();
}
