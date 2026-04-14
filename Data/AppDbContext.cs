using BPCVN.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>      Users      { get; set; }
    public DbSet<Kit>       Kits       { get; set; }
    public DbSet<Switch>    Switches   { get; set; }
    public DbSet<Keycap>    Keycaps    { get; set; }
    public DbSet<Spec>      Specs      { get; set; }
    public DbSet<SoundTest> SoundTests { get; set; }

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
        });

        // ── Switch ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Switch>(entity =>
        {
            entity.ToTable("Switches");
        });

        // ── Keycap ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Keycap>(entity =>
        {
            entity.ToTable("Keycaps");
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

            entity.HasOne(s => s.Switch)
                  .WithMany(sw => sw.Specs)
                  .HasForeignKey(s => s.SwitchId)
                  .OnDelete(DeleteBehavior.Restrict);

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
    }
}
