using System.ComponentModel.DataAnnotations;

namespace BPCVN.Models.Entities;

public class Keycap
{
    [Key]
    public int KeycapId { get; set; }

    [Required(ErrorMessage = "Tên keycap là bắt buộc.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Brand { get; set; }

    /// <summary>Ví dụ: "Cherry", "SA", "KAT", "MT3"</summary>
    [StringLength(50)]
    public string? Profile { get; set; }

    /// <summary>Ví dụ: "ABS", "PBT"</summary>
    [StringLength(50)]
    public string? Material { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>Mô tả keycap — có thể null</summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Cờ xóa mềm — true = đã bị ẩn khỏi hệ thống</summary>
    public bool IsDeleted { get; set; } = false;

    // Navigation
    public ICollection<Spec> Specs { get; set; } = new List<Spec>();
}
