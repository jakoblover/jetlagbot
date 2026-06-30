using JetlagBot.App.Data;
using JetlagBot.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetlagBot.App.Services;

public interface IGuildSettingsService
{
    Task<GuildSettings> GetOrCreateAsync(ulong guildId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuildSettings>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(ulong guildId, int minimumMembershipAgeDays, int vouchCooldownDays, ulong? vouchChannelId, CancellationToken cancellationToken = default);
}

public class GuildSettingsService : IGuildSettingsService
{
    private readonly JetlagBotDbContext _db;

    public GuildSettingsService(JetlagBotDbContext db)
    {
        _db = db;
    }

    public async Task<GuildSettings> GetOrCreateAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var settings = await _db.GuildSettings.FirstOrDefaultAsync(g => g.GuildId == guildId, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new GuildSettings { GuildId = guildId };
        _db.GuildSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<IReadOnlyList<GuildSettings>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.GuildSettings
            .OrderBy(g => g.GuildId)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(ulong guildId, int minimumMembershipAgeDays, int vouchCooldownDays, ulong? vouchChannelId, CancellationToken cancellationToken = default)
    {
        if (minimumMembershipAgeDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumMembershipAgeDays));
        }

        if (vouchCooldownDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vouchCooldownDays));
        }

        var settings = await GetOrCreateAsync(guildId, cancellationToken);
        settings.MinimumMembershipAgeDays = minimumMembershipAgeDays;
        settings.VouchCooldownDays = vouchCooldownDays;
        settings.VouchChannelId = vouchChannelId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
