using JetlagBot.App.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JetlagBot.App.Data;

public class JetlagBotDbContext : DbContext
{
    public JetlagBotDbContext(DbContextOptions<JetlagBotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Vouch> Vouches => Set<Vouch>();

    public DbSet<GuildSettings> GuildSettings => Set<GuildSettings>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Discord snowflake ids are 64-bit unsigned; PostgreSQL has no unsigned 64-bit
        // type, so store them as text to avoid overflow and keep them human-readable.
        var ulongToString = new ValueConverter<ulong, string>(
            v => v.ToString(),
            v => ulong.Parse(v));

        var nullableUlongToString = new ValueConverter<ulong?, string?>(
            v => v.HasValue ? v.Value.ToString() : null,
            v => v != null ? ulong.Parse(v) : null);

        modelBuilder.Entity<Vouch>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.GuildId).HasConversion(ulongToString);
            entity.Property(v => v.TargetUserId).HasConversion(ulongToString);
            entity.Property(v => v.VoucherUserId).HasConversion(ulongToString);
            entity.Property(v => v.TargetDisplayName).HasMaxLength(256).IsRequired();
            entity.Property(v => v.VoucherDisplayName).HasMaxLength(256).IsRequired();
            entity.Property(v => v.Message).HasMaxLength(2000);
            entity.HasIndex(v => new { v.GuildId, v.TargetUserId });
            entity.HasIndex(v => new { v.GuildId, v.VoucherUserId, v.CreatedAtUtc });
        });

        modelBuilder.Entity<GuildSettings>(entity =>
        {
            entity.HasKey(g => g.GuildId);
            entity.Property(g => g.GuildId).HasConversion(ulongToString);
            entity.Property(g => g.VouchChannelId).HasConversion(nullableUlongToString);
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(a => a.DiscordUserId);
            entity.Property(a => a.DiscordUserId).HasConversion(ulongToString);
            entity.Property(a => a.AddedByDiscordUserId).HasConversion(nullableUlongToString);
            entity.Property(a => a.DisplayName).HasMaxLength(256);
        });
    }
}
