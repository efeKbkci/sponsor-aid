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

            // 🚀 SİHİRLİ DOKUNUŞ: Sütunun tipini PostgreSQL'in üstün JSONB formatına ayarlıyoruz
            entity.Property(e => e.JsonData).HasColumnType("jsonb").IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}