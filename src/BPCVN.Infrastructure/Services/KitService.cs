using BPCVN.Application.Common;
using BPCVN.Application.Interfaces;
using BPCVN.Domain.Entities;
using BPCVN.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Infrastructure.Services;

/// <summary>
/// Dịch vụ truy vấn danh sách Kit có tìm kiếm, lọc và phân trang từ cơ sở dữ liệu.
/// </summary>
public class KitService : IKitService
{
    private readonly AppDbContext _db;

    public KitService(AppDbContext db) => _db = db;

    /// <summary>
    /// Lấy danh sách Kit theo từ khóa, thương hiệu, layout và phân trang trực tiếp bằng Skip/Take.
    /// </summary>
    public async Task<PagedResult<Kit>> GetPagedAsync(
        string? searchQuery,
        string? brand,
        string? layout,
        int page = 1,
        int pageSize = 18)
    {
        var query = _db.Kits.AsNoTracking().AsQueryable();

        // Áp dụng tìm kiếm theo tên hoặc thương hiệu (không phân biệt hoa thường)
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var kw = searchQuery.Trim().ToLower();
            query = query.Where(k =>
                k.Name.ToLower().Contains(kw) ||
                (k.Brand != null && k.Brand.ToLower().Contains(kw)));
        }

        // Lọc theo thương hiệu và layout
        if (!string.IsNullOrWhiteSpace(brand))
            query = query.Where(k => k.Brand == brand);

        if (!string.IsNullOrWhiteSpace(layout))
            query = query.Where(k => k.Layout == layout);

        // Sắp xếp ổn định trước khi Skip/Take và nạp kèm ảnh đại diện của Kit
        var orderedQuery = query
            .Include(k => k.KitImages)
            .OrderBy(k => k.Brand)
            .ThenBy(k => k.Name)
            .ThenBy(k => k.KitId);

        return await orderedQuery.ToPagedResultAsync(page, pageSize);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ thương hiệu và layout hiện có để hiển thị trong bộ lọc.
    /// </summary>
    public async Task<(List<string> Brands, List<string> Layouts)> GetFilterOptionsAsync()
    {
        var kits = _db.Kits.AsNoTracking();
        var brands = await kits.Select(k => k.Brand).ToDistinctOptionsAsync();
        var layouts = await kits.Select(k => k.Layout).ToDistinctOptionsAsync();
        return (brands, layouts);
    }
}
