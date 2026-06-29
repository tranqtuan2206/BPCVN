using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BPCVN.Models.Entities;

/// <summary>
/// Model bình luận cho SoundTest — hỗ trợ reply lồng nhau (nested comment).
/// ParentCommentId = null → bình luận gốc; != null → reply cho bình luận cha.
/// </summary>
public class SoundTestComment
{
    [Key]
    public int Id { get; set; }

    /// <summary>Nội dung bình luận</summary>
    [Required(ErrorMessage = "Nội dung bình luận không được để trống.")]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Thời điểm tạo bình luận</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User đã bình luận</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>SoundTest được bình luận</summary>
    [Required]
    public Guid SoundTestId { get; set; }

    /// <summary>
    /// Id bình luận cha (null = bình luận gốc, có giá trị = reply).
    /// Dùng cho quan hệ tự tham chiếu (self-referencing).
    /// </summary>
    public int? ParentCommentId { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(SoundTestId))]
    public SoundTest SoundTest { get; set; } = null!;

    /// <summary>Bình luận cha (null nếu là comment gốc)</summary>
    [ForeignKey(nameof(ParentCommentId))]
    public SoundTestComment? ParentComment { get; set; }

    /// <summary>Danh sách các reply (bình luận con)</summary>
    public ICollection<SoundTestComment> Replies { get; set; } = new List<SoundTestComment>();
}
