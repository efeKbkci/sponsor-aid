using Microsoft.EntityFrameworkCore;

namespace SponsorAid.Infrastructure.Persistence;

public class SponsorAidDbContext : DbContext
{
    public DbSet<DraftRecord> Drafts { get; set; }

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

        base.OnModelCreating(modelBuilder);
    }
}