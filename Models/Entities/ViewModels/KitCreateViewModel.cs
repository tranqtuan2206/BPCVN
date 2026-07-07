using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BPCVN.Models.ViewModels;

/// <summary>
/// ViewModel cho form tạo mới Kit — bao gồm thông tin Kit + upload ảnh.
/// Hỗ trợ đồng thời: upload file lên Cloudinary VÀ nhập link URL trực tiếp.
/// </summary>
public class KitCreateViewModel
{
    [Required(ErrorMessage = "Tên kit là bắt buộc.")]
    [StringLength(100)]
    [Display(Name = "Tên kit")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Thương hiệu")]
    public string? Brand { get; set; }

    /// <summary>Ví dụ: "65%", "75%", "TKL", "Full-size"</summary>
    [StringLength(50)]
    [Display(Name = "Layout")]
    public string? Layout { get; set; }

    /// <summary>Ví dụ: "Gasket Mount", "Top Mount", "Tray Mount"</summary>
    [StringLength(50)]
    [Display(Name = "Kiểu mount")]
    public string? MountType { get; set; }

    /// <summary>Ví dụ: "Hotswap", "Soldered"</summary>
    [StringLength(50)]
    [Display(Name = "Loại PCB")]
    public string? PcbType { get; set; }

    // ── Ảnh Kit ──────────────────────────────────────────────────────────────

    /// <summary>Danh sách file ảnh upload lên Cloudinary</summary>
    [Display(Name = "Upload ảnh")]
    public List<IFormFile>? UploadImages { get; set; }

    /// <summary>Danh sách link URL ảnh nhập tay (external link)</summary>
    [Display(Name = "Link URL ảnh")]
    public List<string>? ExternalImageUrls { get; set; }

    /// <summary>Danh sách mã màu hex cho ảnh upload (index khớp với UploadImages)</summary>
    [Display(Name = "Màu ảnh upload")]
    public List<string>? UploadImageColors { get; set; }

    /// <summary>Danh sách mã màu hex cho ảnh URL (index khớp với ExternalImageUrls)</summary>
    [Display(Name = "Màu ảnh URL")]
    public List<string>? ExternalImageColors { get; set; }
}
