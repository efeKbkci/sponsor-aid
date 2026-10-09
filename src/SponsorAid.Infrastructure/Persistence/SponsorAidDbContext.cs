using Microsoft.EntityFrameworkCore;

namespace SponsorAid.Infrastructure.Persistence;

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
            entity.Property(e => e.JsonData).HasColumnType("jsonb").IsRequired();

            // 🚀 ChatId ve IsActive sorgularını milisaniyelere indiren indeks
            entity.HasIndex(e => new { e.ChatId, e.IsActive });
        });

        modelBuilder.Entity<RegistrationTrackingRecord>(entity =>
        {
            entity.HasKey(e => e.RegistrationKey);
            entity.Property(e => e.GuardianName).HasMaxLength(250);
            entity.Property(e => e.Status).HasConversion<int>().IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}