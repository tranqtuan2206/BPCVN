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

    // Navigation
    public ICollection<Spec> Specs { get; set; } = new List<Spec>();
}
