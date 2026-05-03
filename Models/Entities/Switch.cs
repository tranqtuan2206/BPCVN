using System.ComponentModel.DataAnnotations;

namespace BPCVN.Models.Entities;

public class Switch
{
    [Key]
    public int SwitchId { get; set; }

    [Required(ErrorMessage = "Tên switch là bắt buộc.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Brand { get; set; }

    /// <summary>Giá trị hợp lệ: "Linear" | "Tactile" | "Clicky"</summary>
    [StringLength(20)]
    public string? Type { get; set; }

    /// <summary>Ví dụ: "45g", "67g"</summary>
    [StringLength(20)]
    public string? ActuationForce { get; set; }

    /// <summary>Link hình ảnh minh họa cho switch</summary>
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>Cờ xóa mềm — true = đã bị ẩn khỏi hệ thống</summary>
    public bool IsDeleted { get; set; } = false;

    // Navigation
    public ICollection<Spec> Specs { get; set; } = new List<Spec>();
}
