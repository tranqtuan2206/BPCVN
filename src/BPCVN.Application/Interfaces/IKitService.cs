using BPCVN.Application.Common;
using BPCVN.Domain.Entities;

namespace BPCVN.Application.Interfaces;

/// <summary>
/// Giao diện xử lý truy vấn danh sách và bộ lọc cho danh mục Kit.
/// </summary>
public interface IKitService
{
    /// <summary>
    /// Lấy danh sách Kit có tìm kiếm, lọc và phân trang trực tiếp tại cơ sở dữ liệu.
    /// </summary>
    Task<PagedResult<Kit>> GetPagedAsync(
        string? searchQuery,
        string? brand,
        string? layout,
        int page = 1,
        int pageSize = 18);

    /// <summary>
    /// Lấy danh sách thương hiệu và layout duy nhất để hiển thị trên bộ lọc.
    /// </summary>
    Task<(List<string> Brands, List<string> Layouts)> GetFilterOptionsAsync();
}
