using BPCVN.Application.Common;
using BPCVN.Application.Interfaces;
using BPCVN.Domain.Entities;
using BPCVN.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Infrastructure.Services;

/// <summary>
/// Dịch vụ truy vấn danh sách Keycap có tìm kiếm, lọc và phân trang từ cơ sở dữ liệu.
/// </summary>
public class KeycapService : IKeycapService
{
    private readonly AppDbContext _db;

    public KeycapService(AppDbContext db) => _db = db;

    /// <summary>
    /// Lấy danh sách Keycap theo từ khóa, profile, chất liệu và phân trang trực tiếp bằng Skip/Take.
    /// </summary>
    public async Task<PagedResult<Keycap>> GetPagedAsync(
        string? searchQuery,
        string? profile,
        string? material,
        int page = 1,
        int pageSize = 18)
    {
        var query = _db.Keycaps.AsNoTracking().AsQueryable();

        // Áp dụng tìm kiếm theo tên hoặc thương hiệu (không phân biệt hoa thường)
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var kw = searchQuery.Trim().ToLower();
            query = query.Where(k =>
                k.Name.ToLower().Contains(kw) ||
                (k.Brand != null && k.Brand.ToLower().Contains(kw)));
        }

        // Lọc theo profile và chất liệu
        if (!string.IsNullOrWhiteSpace(profile))
            query = query.Where(k => k.Profile == profile);

        if (!string.IsNullOrWhiteSpace(material))
            query = query.Where(k => k.Material == material);

        // Sắp xếp ổn định trước khi Skip/Take
        var orderedQuery = query
            .OrderBy(k => k.Brand)
            .ThenBy(k => k.Name)
            .ThenBy(k => k.KeycapId);

        return await orderedQuery.ToPagedResultAsync(page, pageSize);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ profile và chất liệu hiện có để hiển thị trong bộ lọc.
    /// </summary>
    public async Task<(List<string> Profiles, List<string> Materials)> GetFilterOptionsAsync()
    {
        var keycaps = _db.Keycaps.AsNoTracking();
        var profiles = await keycaps.Select(k => k.Profile).ToDistinctOptionsAsync();
        var materials = await keycaps.Select(k => k.Material).ToDistinctOptionsAsync();
        return (profiles, materials);
    }
}
