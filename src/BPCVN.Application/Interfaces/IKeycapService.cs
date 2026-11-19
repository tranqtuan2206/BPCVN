using BPCVN.Application.Common;
using BPCVN.Domain.Entities;

namespace BPCVN.Application.Interfaces;

/// <summary>
/// Giao diện xử lý truy vấn danh sách và bộ lọc cho danh mục Keycap.
/// </summary>
public interface IKeycapService
{
    /// <summary>
    /// Lấy danh sách Keycap có tìm kiếm, lọc và phân trang trực tiếp tại cơ sở dữ liệu.
    /// </summary>
    Task<PagedResult<Keycap>> GetPagedAsync(
        string? searchQuery,
        string? profile,
        string? material,
        int page = 1,
        int pageSize = 18);

    /// <summary>
    /// Lấy danh sách profile và chất liệu duy nhất để hiển thị trên bộ lọc.
    /// </summary>
    Task<(List<string> Profiles, List<string> Materials)> GetFilterOptionsAsync();
}
