using Microsoft.EntityFrameworkCore;

namespace SponsorAid.Infrastructure.Persistence;

/// <summary>
/// Entity Framework database context for application persistence in PostgreSQL.
/// </summary>
public class SponsorAidDbContext : DbContext
{
    public DbSet<DraftRecord> Drafts { get; set; }
    public DbSet<RegistrationTrackingRecord> RegistrationTrackings { get; set; }

    public SponsorAidDbContext(DbContextOptions<SponsorAidDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DraftRecord>(entity =>
        {
            entity.HasKey(e => e.RegistrationKey);

            // Stores draft state as PostgreSQL jsonb to support structured JSON storage and querying.
            entity.Property(e => e.JsonData).HasColumnType("jsonb").IsRequired();

            // Composite index improves lookup speed when finding the active draft for a chat user.
            entity.HasIndex(e => new { e.ChatId, e.IsActive });
        });

        modelBuilder.Entity<RegistrationTrackingRecord>(entity =>
        {
            entity.HasKey(e => e.RegistrationKey);

            // Sets maximum length constraint to prevent unexpected oversized strings.
            entity.Property(e => e.GuardianName).HasMaxLength(250);

            // Stores enum values as integers to ensure compact database storage and prevent naming dependency.
            entity.Property(e => e.Status).HasConversion<int>().IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
