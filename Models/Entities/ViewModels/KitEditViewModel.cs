using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BPCVN.Models.ViewModels;

/// <summary>
/// ViewModel cho form chỉnh sửa Kit — đồng bộ ảnh bằng Id.
/// </summary>
public class KitEditViewModel
{
    [Required]
    public int KitId { get; set; }

    [Required(ErrorMessage = "Tên kit là bắt buộc.")]
    [StringLength(100)]
    [Display(Name = "Tên kit")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Thương hiệu")]
    public string? Brand { get; set; }

    [StringLength(50)]
    [Display(Name = "Layout")]
    public string? Layout { get; set; }

    [StringLength(50)]
    [Display(Name = "Kiểu mount")]
    public string? MountType { get; set; }

    [StringLength(50)]
    [Display(Name = "Loại PCB")]
    public string? PcbType { get; set; }

    // ── Ảnh hiện có (giữ lại từ DB) ──────────────────────────────────────

    /// <summary>Id của từng ảnh cũ còn giữ — index khớp với ExistingImageColors</summary>
    public List<int>? ExistingImageIds { get; set; }

    /// <summary>Màu hex của ảnh cũ — index khớp với ExistingImageIds</summary>
    public List<string>? ExistingImageColors { get; set; }

    // ── Ảnh mới ──────────────────────────────────────────────────────────

    /// <summary>File upload mới lên Cloudinary</summary>
    [Display(Name = "Upload ảnh")]
    public List<IFormFile>? UploadImages { get; set; }

    /// <summary>Màu hex cho file upload — index khớp với UploadImages</summary>
    public List<string>? UploadImageColors { get; set; }

    /// <summary>URL ảnh mới nhập tay</summary>
    [Display(Name = "Link URL ảnh")]
    public List<string>? ExternalImageUrls { get; set; }

    /// <summary>Màu hex cho URL mới — index khớp với ExternalImageUrls</summary>
    public List<string>? ExternalImageColors { get; set; }
}
