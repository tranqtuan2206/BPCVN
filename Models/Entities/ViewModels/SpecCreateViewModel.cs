using System.ComponentModel.DataAnnotations;

namespace BPCVN.Models.ViewModels;

public class SpecCreateViewModel
{
    [Required(ErrorMessage = "Tên build là bắt buộc")]
    [StringLength(150)]
    [Display(Name = "Tên build")]
    public string BuildName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên kit")]
    [StringLength(100)]
    [Display(Name = "Kit")]
    public string KitName { get; set; } = string.Empty;

    // ID khi user chọn Switch có sẵn từ datalist, null khi nhập tên mới
    [Display(Name = "Switch")]
    public int? SelectedSwitchId { get; set; }

    // Tên switch — dùng khi user gõ tên (cả có sẵn lẫn tự nhập)
    [StringLength(100)]
    [Display(Name = "Switch")]
    public string? SwitchName { get; set; }

    [StringLength(100)]
    [Display(Name = "Keycap (tuỳ chọn)")]
    public string? KeycapName { get; set; }

    [StringLength(100)]
    [Display(Name = "Vật liệu plate")]
    public string? PlateMaterial { get; set; }

    [StringLength(200)]
    [Display(Name = "Foam setup")]
    public string? FoamSetup { get; set; }

    [StringLength(500)]
    [Display(Name = "Các mod đã thực hiện")]
    public string? Mods { get; set; }
}