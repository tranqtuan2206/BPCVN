namespace BPCVN.Models.Entities;

/// <summary>
/// Trạng thái của một PendingItem trong queue Admin.
/// </summary>
public enum PendingStatus
{
    Pending,   // Chờ Admin xem xét
    Approved,  // Admin đã duyệt → entity IsApproved = true
    Rejected,  // Admin từ chối → entity bị xóa
    Merged     // Admin gộp vào entity có sẵn → entity rác bị xóa
}
