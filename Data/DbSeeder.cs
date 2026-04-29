using BPCVN.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
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
            db.Users.Add(new User
            {
                Username     = "Admin",
                Email        = "admin@twsnwithunikey",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role         = "Admin",
                CreatedAt    = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
