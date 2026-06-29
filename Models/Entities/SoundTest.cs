using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BPCVN.Models.Entities;

public class SoundTest
{
    [Key]
    public Guid TestId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SpecId { get; set; }

    /// <summary>Ví dụ: "Blue Yeti", "Rode NT-USB"</summary>
    [StringLength(100)]
    public string? MicUsed { get; set; }

    /// <summary>Đường dẫn file trên server hoặc URL cloud (R2)</summary>
    [Required(ErrorMessage = "File âm thanh là bắt buộc.")]
    [StringLength(1000)]
    public string AudioUrl { get; set; } = string.Empty;

    public int Upvotes { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(SpecId))]
    public Spec Spec { get; set; } = null!;

    /// <summary>Danh sách lượt Like từ bảng trung gian</summary>
    public ICollection<SoundTestLike> Likes { get; set; } = new List<SoundTestLike>();

    /// <summary>Danh sách bình luận</summary>
    public ICollection<SoundTestComment> Comments { get; set; } = new List<SoundTestComment>();
}
