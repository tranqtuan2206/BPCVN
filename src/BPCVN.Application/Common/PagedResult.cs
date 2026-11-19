namespace BPCVN.Application.Common;

/// <summary>
/// Giao diện mô tả thông tin phân trang dùng chung cho Partial View _Pagination.
/// </summary>
public interface IPagedResult
{
    /// <summary>Số trang hiện tại (bắt đầu từ 1).</summary>
    int PageNumber { get; }

    /// <summary>Số lượng sản phẩm trên mỗi trang.</summary>
    int PageSize { get; }

    /// <summary>Tổng số bản ghi khớp điều kiện lọc.</summary>
    int TotalItems { get; }

    /// <summary>Tổng số trang được tính từ TotalItems và PageSize.</summary>
    int TotalPages { get; }

    /// <summary>Cho biết có thể lùi về trang trước hay không.</summary>
    bool HasPreviousPage { get; }

    /// <summary>Cho biết có thể sang trang tiếp theo hay không.</summary>
    bool HasNextPage { get; }
}

/// <summary>
/// Lớp chứa kết quả phân trang tổng quát cho danh sách dữ liệu.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của phần tử trong trang.</typeparam>
public class PagedResult<T> : IPagedResult
{
    /// <summary>Danh sách phần tử thuộc trang hiện tại.</summary>
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

    /// <summary>Số trang hiện tại (mặc định trang 1).</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Kích thước mỗi trang (mặc định 18 sản phẩm).</summary>
    public int PageSize { get; set; } = 18;

    /// <summary>Tổng số bản ghi khớp điều kiện tìm kiếm và bộ lọc.</summary>
    public int TotalItems { get; set; }

    /// <summary>Tổng số trang tính tự động bằng phép chia làm tròn lên.</summary>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling((double)TotalItems / PageSize)
        : 0;

    /// <summary>Có trang trước khi trang hiện tại lớn hơn 1.</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>Có trang sau khi trang hiện tại nhỏ hơn tổng số trang.</summary>
    public bool HasNextPage => PageNumber < TotalPages;
}
