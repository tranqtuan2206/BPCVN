using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BPCVN.Models.Entities;

/// <summary>
/// Lưu các Kit/Switch/Keycap do user tự tạo tên mới, chờ Admin duyệt.
/// Entity liên quan được tạo trong bảng chính với IsApproved = false.
/// </summary>
public class PendingItem
{
    [Key]
    public int Id { get; set; }

    /// <summary>Loại entity: "Kit" | "Switch" | "Keycap"</summary>
    [Required]
    [StringLength(20)]
    public string Type { get; set; } = string.Empty;

    /// <summary>Tên gốc user nhập (trước normalize)</summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Id của entity đang pending trong bảng Kit/Switch/Keycap (IsApproved=false).
    /// Null sau khi Reject (entity bị xóa).
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>User đã submit tên này</summary>
    [Required]
    public Guid SubmittedByUserId { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public PendingStatus Status { get; set; } = PendingStatus.Pending;

    // Navigation
    [ForeignKey(nameof(SubmittedByUserId))]
    public User SubmittedBy { get; set; } = null!;
}
