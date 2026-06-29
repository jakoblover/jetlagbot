using JetlagBot.App.Configuration;
using JetlagBot.App.Data;
using JetlagBot.App.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JetlagBot.App.Services;

public interface IAdminService
{
    Task<bool> IsAdminAsync(ulong discordUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminUser>> GetAdminsAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<ulong> GetBootstrapAdminIds();

    Task AddAdminAsync(ulong discordUserId, string? displayName, ulong? addedByDiscordUserId, CancellationToken cancellationToken = default);

    Task RemoveAdminAsync(ulong discordUserId, CancellationToken cancellationToken = default);
}

public class AdminService : IAdminService
{
    private readonly JetlagBotDbContext _db;
    private readonly IClock _clock;
    private readonly HashSet<ulong> _bootstrapAdminIds;

    public AdminService(JetlagBotDbContext db, IClock clock, IOptions<AdminOptions> adminOptions)
    {
        _db = db;
        _clock = clock;
        _bootstrapAdminIds = ParseBootstrapIds(adminOptions.Value.DiscordUserIds);
    }

    public async Task<bool> IsAdminAsync(ulong discordUserId, CancellationToken cancellationToken = default)
    {
        if (_bootstrapAdminIds.Contains(discordUserId))
        {
            return true;
        }

        return await _db.AdminUsers.AnyAsync(a => a.DiscordUserId == discordUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUser>> GetAdminsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.AdminUsers
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public IReadOnlyList<ulong> GetBootstrapAdminIds() => _bootstrapAdminIds.ToList();

    public async Task AddAdminAsync(ulong discordUserId, string? displayName, ulong? addedByDiscordUserId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.AdminUsers.FirstOrDefaultAsync(a => a.DiscordUserId == discordUserId, cancellationToken);
        if (existing is not null)
        {
            existing.DisplayName = displayName ?? existing.DisplayName;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        _db.AdminUsers.Add(new AdminUser
        {
            DiscordUserId = discordUserId,
            DisplayName = displayName,
            CreatedAtUtc = _clock.UtcNow,
            AddedByDiscordUserId = addedByDiscordUserId,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAdminAsync(ulong discordUserId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.AdminUsers.FirstOrDefaultAsync(a => a.DiscordUserId == discordUserId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _db.AdminUsers.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static HashSet<ulong> ParseBootstrapIds(IEnumerable<string> rawIds)
    {
        var ids = new HashSet<ulong>();
        foreach (var raw in rawIds)
        {
            if (ulong.TryParse(raw?.Trim(), out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
