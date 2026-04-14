using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BPCVN.Models.Entities;

public class Spec
{
    [Key]
    public Guid SpecId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public int KitId { get; set; }

    [Required]
    public int SwitchId { get; set; }

    public int? KeycapId { get; set; }

    [Required(ErrorMessage = "Tên build là bắt buộc.")]
    [StringLength(150)]
    public string BuildName { get; set; } = string.Empty;

    /// <summary>Ví dụ: "Aluminium", "PC", "Carbon Fiber"</summary>
    [StringLength(100)]
    public string? PlateMaterial { get; set; }

    /// <summary>Ví dụ: "Case foam + PCB foam + PE foam"</summary>
    [StringLength(200)]
    public string? FoamSetup { get; set; }

    /// <summary>Tape mod, tempest mod... — có thể null</summary>
    [StringLength(500)]
    public string? Mods { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(KitId))]
    public Kit Kit { get; set; } = null!;

    [ForeignKey(nameof(SwitchId))]
    public Switch Switch { get; set; } = null!;

    [ForeignKey(nameof(KeycapId))]
    public Keycap? Keycap { get; set; }

    public ICollection<SoundTest> SoundTests { get; set; } = new List<SoundTest>();
}
