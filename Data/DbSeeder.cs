using BPCVN.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BPCVN.Data;

/// <summary>
/// Lớp chịu trách nhiệm khởi tạo dữ liệu mẫu (Seed Data) cho database.
/// Sử dụng IConfiguration để đọc cấu hình thay vì hardcode giá trị nhạy cảm.
/// </summary>
public class DbSeeder
{
    // Dependency: đối tượng cấu hình để đọc appsettings
    private readonly IConfiguration _config;

    /// <summary>
    /// Constructor — nhận IConfiguration thông qua Dependency Injection.
    /// </summary>
    public DbSeeder(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Thực hiện seed dữ liệu mẫu vào database.
    /// </summary>
    public async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        // ── Kits ─────────────────────────────────────────────────────────────
        if (!await db.Kits.AnyAsync())
        {
            db.Kits.AddRange(
                new Kit
                {
                    Name = "Hiexa V65",
                    Brand = "Hiexa",
                    Layout = "65%",
                    MountType = "Gasket Mount",
                    PcbType = "Hotswap"
                },
                new Kit
                {
                    Name = "Neo65",
                    Brand = "NeoStudio",
                    Layout = "65%",
                    MountType = "Gasket Mount",
                    PcbType = "Hotswap"
                },
                new Kit
                {
                    Name = "QK75",
                    Brand = "QwertyKeys",
                    Layout = "75%",
                    MountType = "Top Mount",
                    PcbType = "Hotswap"
                },
                new Kit
                {
                    Name = "Zoom65 V2",
                    Brand = "Meletrix",
                    Layout = "65%",
                    MountType = "Gasket Mount",
                    PcbType = "Hotswap"
                },
                new Kit
                {
                    Name = "KBD67 Lite R4",
                    Brand = "KBDFans",
                    Layout = "65%",
                    MountType = "Gasket Mount",
                    PcbType = "Hotswap"
                }
            );
        }

        // ── Switches ─────────────────────────────────────────────────────────
        if (!await db.Switches.AnyAsync())
        {
            db.Switches.AddRange(
                new Switch
                {
                    Name = "Hyacinth V2",
                    Brand = "HMX",
                    Type = "Linear",
                    ActuationForce = "35g"
                },
                new Switch
                {
                    Name = "KTT Kang White",
                    Brand = "KTT",
                    Type = "Linear",
                    ActuationForce = "45g"
                },
                new Switch
                {
                    Name = "Cherry MX Black",
                    Brand = "Cherry",
                    Type = "Linear",
                    ActuationForce = "60g"
                },
                new Switch
                {
                    Name = "Gateron Yellow Pro",
                    Brand = "Gateron",
                    Type = "Linear",
                    ActuationForce = "35g"
                },
                new Switch
                {
                    Name = "Boba U4T",
                    Brand = "Gazzew",
                    Type = "Tactile",
                    ActuationForce = "62g"
                },
                new Switch
                {
                    Name = "Holy Panda X",
                    Brand = "Drop",
                    Type = "Tactile",
                    ActuationForce = "67g"
                }
            );
        }

        // ── Keycaps ──────────────────────────────────────────────────────────
        if (!await db.Keycaps.AnyAsync())
        {
            db.Keycaps.AddRange(
                new Keycap
                {
                    Name = "GMK Nord",
                    Brand = "GMK",
                    Profile = "Cherry",
                    Material = "ABS"
                },
                new Keycap
                {
                    Name = "Domikey Sushi",
                    Brand = "Domikey",
                    Profile = "Cherry",
                    Material = "ABS"
                },
                new Keycap
                {
                    Name = "EPBT BoW",
                    Brand = "ePBT",
                    Profile = "Cherry",
                    Material = "PBT"
                }
            );
        }

        // ── Admin Account ────────────────────────────────────────────────────
        // Tạo tài khoản Admin mặc định nếu chưa tồn tại
        if (!await db.Users.AnyAsync(u => u.Email == "admin@twsnwithunikey"))
        {
            // Đọc mật khẩu Admin từ cấu hình (appsettings.json) thay vì hardcode
            var adminPassword = _config["AdminSettings:DefaultPassword"];

            // ── Bảo mật: chặn các password yếu / placeholder ────────────────
            var weakPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "password", "Password", "123456", "admin", "Admin",
                "admin123", "test", "secret", "[PASSWORD]",
                "[REDACTED — set via Environment Variable: AdminSettings__DefaultPassword]"
            };

            if (string.IsNullOrWhiteSpace(adminPassword) || weakPasswords.Contains(adminPassword))
            {
                // Generate random strong password và in ra console để admin biết
                adminPassword = GenerateRandomPassword();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  [SECURITY] Admin password tự động được tạo:            ║");
                Console.WriteLine($"║  Password: {adminPassword,-46}║");
                Console.WriteLine("║  Hãy đổi ngay sau lần đăng nhập đầu tiên!               ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
                Console.ResetColor();
            }

            db.Users.Add(new User
            {
                Username         = "Admin",
                Email            = "admin@twsnwithunikey",
                PasswordHash     = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role             = "Admin",
                CreatedAt        = DateTime.UtcNow,
                IsEmailConfirmed = true // Admin luôn được kích hoạt sẵn
            });
        }

        await db.SaveChangesAsync();
    }

    // ── Helper: Sinh mật khẩu ngẫu nhiên đủ mạnh ────────────────────────────
    private static string GenerateRandomPassword(int length = 16)
    {
        const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower   = "abcdefghjkmnpqrstuvwxyz";
        const string digits  = "23456789";
        const string special = "!@#$%^&*";
        const string all     = upper + lower + digits + special;

        var rng  = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);

        // Đảm bảo có ít nhất 1 ký tự từ mỗi nhóm
        var chars = new char[length];
        chars[0] = upper[bytes[0]  % upper.Length];
        chars[1] = lower[bytes[1]  % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = special[bytes[3] % special.Length];

        for (int i = 4; i < length; i++)
            chars[i] = all[bytes[i] % all.Length];

        // Xáo trộn để không bị đoán vị trí cố định
        return new string(chars.OrderBy(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(length)).ToArray());
    }
}
