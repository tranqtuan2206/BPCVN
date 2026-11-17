namespace BPCVN.Application.DTOs;

/// <summary>DTO nhận JSON body khi người dùng nhấn thích/bỏ thích SoundTest.</summary>
public record UpvoteRequest(Guid TestId);

/// <summary>DTO nhận JSON body khi thêm bình luận hoặc trả lời bình luận SoundTest.</summary>
public record AddCommentRequest(Guid SoundTestId, string Content, int? ParentCommentId);

/// <summary>DTO nhận JSON body khi xóa bình luận SoundTest.</summary>
public record DeleteCommentRequest(int CommentId);

/// <summary>DTO nhận JSON body khi chỉnh sửa nội dung bình luận SoundTest.</summary>
public record EditCommentRequest(int CommentId, string Content);
