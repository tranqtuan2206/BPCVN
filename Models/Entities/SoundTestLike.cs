using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BPCVN.Models.Entities;

/// <summary>
/// Bảng trung gian lưu lượt Like của User cho SoundTest.
/// Mỗi cặp (UserId, SoundTestId) chỉ tồn tại 1 dòng → 1 user chỉ like 1 lần.
/// </summary>
public class SoundTestLike
{
    [Key]
    public int Id { get; set; }

    /// <summary>User đã like</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>SoundTest được like</summary>
    [Required]
    public Guid SoundTestId { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(SoundTestId))]
    public SoundTest SoundTest { get; set; } = null!;
}
