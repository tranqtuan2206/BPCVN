using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BPCVN.Models.Entities;

/// <summary>
/// Đại diện cho một ảnh của Kit — mỗi Kit có thể có nhiều ảnh.
/// Ảnh có thể từ Cloudinary (upload) hoặc external URL (nhập tay).
/// </summary>
public class KitImage
{
    [Key]
    public int Id { get; set; }

    /// <summary>Khóa ngoại tham chiếu Kit sở hữu ảnh này</summary>
    [Required]
    public int KitId { get; set; }

    /// <summary>Đường dẫn ảnh (Cloudinary URL hoặc external URL)</summary>
    [Required]
    [StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Thứ tự hiển thị ảnh (0 = ảnh đầu tiên)</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>Mã màu hex của ảnh (VD: #FFFFFF) — dùng làm accent color khi hiển thị</summary>
    [StringLength(7)]
    public string? ColorHex { get; set; }

    // Navigation
    [ForeignKey(nameof(KitId))]
    public Kit? Kit { get; set; }
}
