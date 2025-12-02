using Microsoft.EntityFrameworkCore;
using VoteService.Models;

namespace VoteService.Data;

public class VoteDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Vote> Votes { get; set; }
    public DbSet<VoteAI> VoteAIs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vote>(x =>
        {
            x.HasIndex(v => new { v.UserId, v.TargetType, v.TargetId }).IsUnique();
        });

        modelBuilder.Entity<VoteAI>(x =>
        {
            // Ensure a user can only vote once per AI answer target
            x.HasIndex(v => new { v.UserId, v.TargetType, v.TargetId, v.AiId }).IsUnique();
        });
    }
}
