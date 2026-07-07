using System.ComponentModel.DataAnnotations;

namespace BPCVN.Models.Entities;

public class Kit
{
    [Key]
    public int KitId { get; set; }

    [Required(ErrorMessage = "Tên kit là bắt buộc.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Brand { get; set; }

    /// <summary>Ví dụ: "65%", "75%", "TKL", "Full-size"</summary>
    [StringLength(50)]
    public string? Layout { get; set; }

    /// <summary>Ví dụ: "Gasket Mount", "Top Mount", "Tray Mount"</summary>
    [StringLength(50)]
    public string? MountType { get; set; }

    /// <summary>Ví dụ: "Hotswap", "Soldered"</summary>
    [StringLength(50)]
    public string? PcbType { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>Cờ xóa mềm — true = đã bị ẩn khỏi hệ thống</summary>
    public bool IsDeleted { get; set; } = false;

    // Navigation
    public ICollection<Spec> Specs { get; set; } = new List<Spec>();
    public ICollection<KitImage> KitImages { get; set; } = new List<KitImage>();
}
