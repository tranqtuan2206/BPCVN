using BPCVN.Application.Common;
using BPCVN.Application.Interfaces;
using BPCVN.Domain.Entities;
using BPCVN.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Infrastructure.Services;

/// <summary>
/// Dịch vụ truy vấn danh sách Switch có tìm kiếm, lọc và phân trang từ cơ sở dữ liệu.
/// </summary>
public class SwitchService : ISwitchService
{
    private readonly AppDbContext _db;

    public SwitchService(AppDbContext db) => _db = db;

    /// <summary>
    /// Lấy danh sách Switch theo từ khóa, loại, thương hiệu và phân trang trực tiếp bằng Skip/Take.
    /// </summary>
    public async Task<PagedResult<Switch>> GetPagedAsync(
        string? searchQuery,
        string? type,
        string? brand,
        int page = 1,
        int pageSize = 18)
    {
        var query = _db.Switches.AsNoTracking().AsQueryable();

        // Áp dụng tìm kiếm theo tên hoặc thương hiệu (không phân biệt hoa thường)
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var kw = searchQuery.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(kw) ||
                (s.Brand != null && s.Brand.ToLower().Contains(kw)));
        }

        // Lọc theo loại switch và thương hiệu
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(s => s.Type == type);

        if (!string.IsNullOrWhiteSpace(brand))
            query = query.Where(s => s.Brand == brand);

        // Sắp xếp ổn định trước khi Skip/Take
        var orderedQuery = query
            .OrderBy(s => s.Type)
            .ThenBy(s => s.Name)
            .ThenBy(s => s.SwitchId);

        return await orderedQuery.ToPagedResultAsync(page, pageSize);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ loại switch và thương hiệu hiện có để hiển thị trong bộ lọc.
    /// </summary>
    public async Task<(List<string> Types, List<string> Brands)> GetFilterOptionsAsync()
    {
        var switches = _db.Switches.AsNoTracking();
        var types = await switches.Select(s => s.Type).ToDistinctOptionsAsync();
        var brands = await switches.Select(s => s.Brand).ToDistinctOptionsAsync();
        return (types, brands);
    }
}
