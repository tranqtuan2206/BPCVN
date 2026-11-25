using System.Text;

namespace BPCVN.Infrastructure.Services;

/// <summary>
/// Lớp tiện ích bảo mật tập trung cho upload media/ảnh, kiểm tra magic bytes,
/// kiểm tra URL an toàn và chống Path Traversal khi thao tác file cục bộ.
/// </summary>
public static class MediaSecurityHelper
{
    public static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".aac", ".flac", ".ogg", ".wav", ".bwf", ".aiff", ".dsf", ".dff", ".alac"
    };

    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".mxf"
    };

    public static readonly HashSet<string> AllowedMediaExtensions = new(
        AudioExtensions.Concat(VideoExtensions),
        StringComparer.OrdinalIgnoreCase);

    // Không cho phép .svg vì file SVG có thể nhúng script gây XSS
    public static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".avif"
    };

    public const long MaxAudioSizeBytes = 100L * 1024 * 1024; // 100 MB
    public const long MaxVideoSizeBytes = 200L * 1024 * 1024; // 200 MB
    public const long MaxImageSizeBytes = 10L * 1024 * 1024;  // 10 MB
    public const int MaxChunkCount = 15;                      // Tối đa ~270MB với chunk 18MB

    private const string LocalSoundTestPrefix = "/uploads/soundtests/";

    /// <summary>
    /// Chuẩn hóa phần mở rộng file về chữ thường, loại bỏ khoảng trắng.
    /// </summary>
    public static string NormalizeExtension(string? ext)
        => string.IsNullOrWhiteSpace(ext) ? string.Empty : ext.Trim().ToLowerInvariant();

    /// <summary>
    /// Kiểm tra phần mở rộng có thuộc danh sách audio/video được phép hay không.
    /// </summary>
    public static bool IsAllowedMediaExtension(string? ext)
    {
        var normalized = NormalizeExtension(ext);
        return !string.IsNullOrEmpty(normalized) && AllowedMediaExtensions.Contains(normalized);
    }

    /// <summary>
    /// Kiểm tra phần mở rộng có phải là video hay không.
    /// </summary>
    public static bool IsVideoExtension(string? ext)
    {
        var normalized = NormalizeExtension(ext);
        return !string.IsNullOrEmpty(normalized) && VideoExtensions.Contains(normalized);
    }

    /// <summary>
    /// Kiểm tra phần mở rộng ảnh có hợp lệ hay không (đã loại trừ .svg).
    /// </summary>
    public static bool IsAllowedImageExtension(string? ext)
    {
        var normalized = NormalizeExtension(ext);
        return !string.IsNullOrEmpty(normalized) && AllowedImageExtensions.Contains(normalized);
    }

    /// <summary>
    /// Kiểm tra chữ ký đầu file (Magic Bytes) của audio/video nhằm chặn file giả mạo đổi đuôi.
    /// </summary>
    public static bool IsValidMediaSignature(Stream stream, string? ext)
    {
        var normalized = NormalizeExtension(ext);
        if (!IsAllowedMediaExtension(normalized))
            return false;

        var originalPos = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek)
                stream.Position = 0;

            var buffer = new byte[64];
            var read = ReadFully(stream, buffer);
            if (read < 3)
                return false;

            var head = buffer.AsSpan(0, read);

            // Từ chối rõ ràng các định dạng thực thi / tài liệu / script giả danh media
            if (HasDangerousHeader(head))
                return false;

            return normalized switch
            {
                ".mp3" => IsMp3Signature(head),
                ".wav" or ".bwf" => read >= 12 && MatchesAscii(head, 0, "RIFF") && MatchesAscii(head, 8, "WAVE"),
                ".aiff" => read >= 12 && MatchesAscii(head, 0, "FORM") &&
                           (MatchesAscii(head, 8, "AIFF") || MatchesAscii(head, 8, "AIFC")),
                ".flac" => read >= 4 && MatchesAscii(head, 0, "fLaC"),
                ".ogg" => read >= 4 && MatchesAscii(head, 0, "OggS"),
                ".mp4" or ".m4a" or ".alac" => read >= 8 && MatchesAscii(head, 4, "ftyp"),
                ".mov" => read >= 8 && (
                    MatchesAscii(head, 4, "ftyp") ||
                    MatchesAscii(head, 4, "moov") ||
                    MatchesAscii(head, 4, "mdat") ||
                    MatchesAscii(head, 4, "wide") ||
                    MatchesAscii(head, 4, "free") ||
                    MatchesAscii(head, 4, "skip") ||
                    MatchesAscii(head, 4, "pnot")),
                ".aac" => IsAacSignature(head),
                ".mkv" or ".webm" => read >= 4 && head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3,
                ".avi" => read >= 12 && MatchesAscii(head, 0, "RIFF") && MatchesAscii(head, 8, "AVI "),
                ".wmv" => read >= 8 && head[0] == 0x30 && head[1] == 0x26 && head[2] == 0xB2 && head[3] == 0x75 &&
                                       head[4] == 0x8E && head[5] == 0x66 && head[6] == 0xCF && head[7] == 0x11,
                ".mxf" => read >= 4 && head[0] == 0x06 && head[1] == 0x0E && head[2] == 0x2B && head[3] == 0x34,
                ".dsf" => read >= 4 && MatchesAscii(head, 0, "DSD "),
                ".dff" => read >= 4 && MatchesAscii(head, 0, "FRM8"),
                _ => false
            };
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = originalPos;
        }
    }

    /// <summary>
    /// Kiểm tra chữ ký đầu file (Magic Bytes) của ảnh nhằm chặn file giả mạo hoặc SVG.
    /// </summary>
    public static bool IsValidImageSignature(Stream stream, string? ext)
    {
        var normalized = NormalizeExtension(ext);
        if (!IsAllowedImageExtension(normalized))
            return false;

        var originalPos = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek)
                stream.Position = 0;

            var buffer = new byte[16];
            var read = ReadFully(stream, buffer);
            if (read < 2)
                return false;

            var head = buffer.AsSpan(0, read);

            if (HasDangerousHeader(head))
                return false;

            return normalized switch
            {
                ".jpg" or ".jpeg" => read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
                ".png" => read >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 &&
                                       head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A,
                ".gif" => read >= 6 && (MatchesAscii(head, 0, "GIF87a") || MatchesAscii(head, 0, "GIF89a")),
                ".webp" => read >= 12 && MatchesAscii(head, 0, "RIFF") && MatchesAscii(head, 8, "WEBP"),
                ".bmp" => read >= 2 && MatchesAscii(head, 0, "BM"),
                ".avif" => read >= 12 && MatchesAscii(head, 4, "ftyp") && (
                    MatchesAscii(head, 8, "avif") ||
                    MatchesAscii(head, 8, "avis") ||
                    MatchesAscii(head, 8, "mif1") ||
                    MatchesAscii(head, 8, "msf1")),
                _ => false
            };
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = originalPos;
        }
    }

    /// <summary>
    /// Kiểm tra URL tuyệt đối chỉ cho phép giao thức http:// hoặc https:// (chặn javascript:, data:, file:).
    /// </summary>
    public static bool IsSafeHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kiểm tra URL ảnh cho Switch/Keycap: cho phép http(s) hoặc đường dẫn tương đối nội bộ an toàn bắt đầu bằng '/'.
    /// </summary>
    public static bool IsSafeImageUrlOrLocalPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var trimmed = url.Trim();
        if (trimmed.StartsWith('/') && !trimmed.StartsWith("//") && !trimmed.Contains(".."))
            return true;

        return IsSafeHttpUrl(trimmed);
    }

    /// <summary>
    /// Chuẩn hóa và kiểm tra đường dẫn con phải nằm hoàn toàn bên trong thư mục gốc (chống Path Traversal).
    /// Trả về null nếu đường dẫn thoát ra ngoài thư mục gốc.
    /// </summary>
    public static string? ResolveSafeLocalPath(string rootDir, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootDir) || string.IsNullOrWhiteSpace(relativePath))
            return null;

        var trimmed = relativePath.TrimStart('/', '\\');
        if (string.IsNullOrWhiteSpace(trimmed) || Path.IsPathRooted(trimmed))
            return null;

        var fullRoot = Path.GetFullPath(rootDir);
        var fullRootWithSep = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        var normalizedRelative = trimmed.Replace('/', Path.DirectorySeparatorChar)
                                        .Replace('\\', Path.DirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelative));

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return combined.StartsWith(fullRootWithSep, comparison) ? combined : null;
    }

    /// <summary>
    /// Xóa an toàn file âm thanh cục bộ thuộc thư mục wwwroot/uploads/soundtests.
    /// Bỏ qua URL Cloudinary (http/https) và từ chối mọi đường dẫn thoát khỏi thư mục cho phép.
    /// </summary>
    public static bool TryDeleteSafeLocalAudio(string webRootPath, string? audioUrl)
    {
        if (string.IsNullOrWhiteSpace(webRootPath) || string.IsNullOrWhiteSpace(audioUrl))
            return false;

        var trimmed = audioUrl.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Chỉ chấp nhận đường dẫn nằm dưới /uploads/soundtests/
        var normalizedUrl = trimmed.Replace('\\', '/');
        if (!normalizedUrl.StartsWith('/'))
            normalizedUrl = "/" + normalizedUrl;

        if (!normalizedUrl.StartsWith(LocalSoundTestPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var subRelative = normalizedUrl[LocalSoundTestPrefix.Length..];
        var soundTestDir = Path.Combine(webRootPath, "uploads", "soundtests");
        var safePath = ResolveSafeLocalPath(soundTestDir, subRelative);

        if (safePath == null || !File.Exists(safePath))
            return false;

        File.Delete(safePath);
        return true;
    }

    // ── Helper nội bộ kiểm tra chữ ký file ──────────────────────────────────

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int n = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (n == 0) break;
            totalRead += n;
        }
        return totalRead;
    }

    private static bool MatchesAscii(ReadOnlySpan<byte> data, int offset, string ascii)
    {
        if (data.Length < offset + ascii.Length)
            return false;

        for (int i = 0; i < ascii.Length; i++)
        {
            if (data[offset + i] != (byte)ascii[i])
                return false;
        }
        return true;
    }

    private static bool HasDangerousHeader(ReadOnlySpan<byte> head)
    {
        // Chặn PE/EXE (MZ), ELF, ZIP/JAR (PK\x03\x04), PDF (%PDF)
        if (head.Length >= 2 && head[0] == 0x4D && head[1] == 0x5A) return true;
        if (head.Length >= 4 && head[0] == 0x7F && head[1] == (byte)'E' && head[2] == (byte)'L' && head[3] == (byte)'F') return true;
        if (head.Length >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04) return true;
        if (head.Length >= 4 && MatchesAscii(head, 0, "%PDF")) return true;

        // Bỏ qua UTF-8 BOM và khoảng trắng đầu file để phát hiện HTML/XML/SVG/PHP/Script
        int idx = 0;
        if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
            idx = 3;

        while (idx < head.Length && (head[idx] == (byte)' ' || head[idx] == (byte)'\t' || head[idx] == (byte)'\r' || head[idx] == (byte)'\n'))
            idx++;

        if (idx < head.Length)
        {
            var textPrefix = Encoding.ASCII.GetString(head[idx..Math.Min(head.Length, idx + 16)]).ToLowerInvariant();
            if (textPrefix.StartsWith("<html") ||
                textPrefix.StartsWith("<!doc") ||
                textPrefix.StartsWith("<svg") ||
                textPrefix.StartsWith("<script") ||
                textPrefix.StartsWith("<?xml") ||
                textPrefix.StartsWith("<?php"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMp3Signature(ReadOnlySpan<byte> head)
    {
        // Tag ID3v2
        if (head.Length >= 3 && MatchesAscii(head, 0, "ID3"))
            return true;

        // MPEG Audio Frame Sync: 11 bit 1 (0xFF + 0xE0), loại trừ MPEG version (01) và Layer (00) reserved
        if (head.Length >= 2 && head[0] == 0xFF && (head[1] & 0xE0) == 0xE0)
        {
            bool validVersion = (head[1] & 0x18) != 0x08;
            bool validLayer = (head[1] & 0x06) != 0x00;
            return validVersion && validLayer;
        }

        return false;
    }

    private static bool IsAacSignature(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && MatchesAscii(head, 0, "ID3"))
            return true;

        if (head.Length >= 4 && MatchesAscii(head, 0, "ADIF"))
            return true;

        // ADTS header: 12 bit sync (0xFF + 0xF0) với layer = 00 -> 0xFF 0xF1 hoặc 0xFF 0xF9
        return head.Length >= 2 && head[0] == 0xFF && (head[1] == 0xF1 || head[1] == 0xF9);
    }
}
