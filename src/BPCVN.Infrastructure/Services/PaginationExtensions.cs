using BPCVN.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Infrastructure.Services;

/// <summary>
/// Các phương thức mở rộng hỗ trợ phân trang và lấy dữ liệu bộ lọc trên IQueryable.
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Thực hiện đếm tổng số bản ghi và cắt trang trực tiếp tại cơ sở dữ liệu bằng Skip/Take.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page = 1,
        int pageSize = 18)
    {
        // Đảm bảo tham số trang và kích thước trang luôn hợp lệ
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 18;

        // Đếm tổng số bản ghi khớp điều kiện tìm kiếm/lọc trước khi cắt trang
        var totalItems = await query.CountAsync();

        // Kẹp số trang hiện tại nếu vượt quá tổng số trang thực tế
        var totalPages = totalItems > 0
            ? (int)Math.Ceiling((double)totalItems / pageSize)
            : 1;
        if (page > totalPages) page = totalPages;

        // Cắt trang trực tiếp bằng SQL OFFSET/FETCH
        var items = totalItems == 0
            ? new List<T>()
            : await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    /// <summary>
    /// Lấy danh sách giá trị chuỗi duy nhất, khác null và sắp xếp tăng dần cho dropdown lọc.
    /// </summary>
    public static Task<List<string>> ToDistinctOptionsAsync(this IQueryable<string?> query)
        => query.Where(v => v != null)
                .Select(v => v!)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();
}
