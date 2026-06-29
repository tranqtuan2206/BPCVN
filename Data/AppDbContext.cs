using BPCVN.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Kit> Kits { get; set; }
    public DbSet<Switch> Switches { get; set; }
    public DbSet<Keycap> Keycaps { get; set; }
    public DbSet<Spec> Specs { get; set; }
    public DbSet<SoundTest> SoundTests { get; set; }
    public DbSet<SoundTestLike> SoundTestLikes { get; set; }
    public DbSet<SoundTestComment> SoundTestComments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasIndex(u => u.Email)
                  .IsUnique()
                  .HasDatabaseName("IX_Users_Email");

            entity.HasIndex(u => u.Username)
                  .IsUnique()
                  .HasDatabaseName("IX_Users_Username");

            entity.Property(u => u.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        // ── Kit ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<Kit>(entity =>
        {
            entity.ToTable("Kits");
            // Global Query Filter: tự động ẩn Kit đã bị xóa mềm
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ── Switch ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Switch>(entity =>
        {
            entity.ToTable("Switches");
            // Global Query Filter: tự động ẩn Switch đã bị xóa mềm
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ── Keycap ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Keycap>(entity =>
        {
            entity.ToTable("Keycaps");
            // Global Query Filter: tự động ẩn Keycap đã bị xóa mềm
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ── Spec ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Spec>(entity =>
        {
            entity.ToTable("Specs");

            entity.HasOne(s => s.User)
                  .WithMany(u => u.Specs)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Kit)
                  .WithMany(k => k.Specs)
                  .HasForeignKey(s => s.KitId)
                  .OnDelete(DeleteBehavior.Restrict);

            // FK Switch — optional: null khi user dùng CustomSwitchName
            entity.HasOne(s => s.Switch)
                  .WithMany(sw => sw.Specs)
                  .HasForeignKey(s => s.SwitchId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(s => s.Keycap)
                  .WithMany(kc => kc.Specs)
                  .HasForeignKey(s => s.KeycapId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(s => s.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        // ── SoundTest ────────────────────────────────────────────────────────
        modelBuilder.Entity<SoundTest>(entity =>
        {
            entity.ToTable("SoundTests");

            entity.HasOne(st => st.Spec)
                  .WithMany(s => s.SoundTests)
                  .HasForeignKey(st => st.SpecId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(st => st.Upvotes)
                  .HasDefaultValue(0);

            entity.Property(st => st.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        // ── SoundTestLike ────────────────────────────────────────────────────
        // Bảng trung gian: 1 user chỉ like 1 SoundTest 1 lần
        modelBuilder.Entity<SoundTestLike>(entity =>
        {
            entity.ToTable("SoundTestLikes");

            // Unique index đảm bảo mỗi cặp (UserId, SoundTestId) chỉ có 1 dòng
            entity.HasIndex(l => new { l.UserId, l.SoundTestId })
                  .IsUnique()
                  .HasDatabaseName("IX_SoundTestLikes_UserId_SoundTestId");

            entity.HasOne(l => l.User)
                  .WithMany(u => u.SoundTestLikes)
                  .HasForeignKey(l => l.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.SoundTest)
                  .WithMany(st => st.Likes)
                  .HasForeignKey(l => l.SoundTestId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SoundTestComment ─────────────────────────────────────────────────
        // Bình luận lồng nhau: self-referencing qua ParentCommentId
        modelBuilder.Entity<SoundTestComment>(entity =>
        {
            entity.ToTable("SoundTestComments");

            entity.HasOne(c => c.User)
                  .WithMany(u => u.SoundTestComments)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict); // Không xóa user khi còn comment

            entity.HasOne(c => c.SoundTest)
                  .WithMany(st => st.Comments)
                  .HasForeignKey(c => c.SoundTestId)
                  .OnDelete(DeleteBehavior.Cascade); // Xóa SoundTest → xóa luôn comment

            // Quan hệ tự tham chiếu: bình luận cha ↔ replies
            entity.HasOne(c => c.ParentComment)
                  .WithMany(c => c.Replies)
                  .HasForeignKey(c => c.ParentCommentId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict); // Tránh cascade cycle trên SQL Server

            entity.Property(c => c.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });
    }
}

