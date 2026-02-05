using Microsoft.EntityFrameworkCore;
using McpVersionVer2.Data.Entities;

namespace McpVersionVer2.Data;

public class ConversationDbContext : DbContext
{
    public DbSet<SessionEntity> Sessions { get; set; }
    public DbSet<ConversationEntryEntity> ConversationEntries { get; set; }
    public DbSet<ConversationSummaryEntity> ConversationSummaries { get; set; }

    public ConversationDbContext(DbContextOptions<ConversationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionEntity>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.HasIndex(e => e.LastAccessedAt);
            entity.HasIndex(e => e.BearerTokenHash)
                  .HasFilter("BearerTokenHash IS NOT NULL");
        });

        modelBuilder.Entity<ConversationEntryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionId, e.Timestamp });
            entity.HasOne<SessionEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationSummaryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionId, e.SummarySequence })
                  .IsUnique();
            entity.HasIndex(e => e.SessionId);
            entity.HasOne<SessionEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
