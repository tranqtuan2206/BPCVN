using BPCVN.Application.Common;
using BPCVN.Domain.Entities;

namespace BPCVN.Application.Interfaces;

/// <summary>
/// Giao diện xử lý truy vấn danh sách và bộ lọc cho danh mục Switch.
/// </summary>
public interface ISwitchService
{
    /// <summary>
    /// Lấy danh sách Switch có tìm kiếm, lọc và phân trang trực tiếp tại cơ sở dữ liệu.
    /// </summary>
    Task<PagedResult<Switch>> GetPagedAsync(
        string? searchQuery,
        string? type,
        string? brand,
        int page = 1,
        int pageSize = 18);

    /// <summary>
    /// Lấy danh sách loại switch và thương hiệu duy nhất để hiển thị trên bộ lọc.
    /// </summary>
    Task<(List<string> Types, List<string> Brands)> GetFilterOptionsAsync();
}
