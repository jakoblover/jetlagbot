using JetlagBot.App.Data;
using JetlagBot.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetlagBot.App.Services;

public interface IVouchService
{
    Task<VouchResult> CreateVouchAsync(CreateVouchRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vouch>> GetVouchesAsync(ulong guildId, ulong targetUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vouch>> GetGuildVouchesAsync(ulong guildId, CancellationToken cancellationToken = default);

    Task<bool> DeleteVouchAsync(int vouchId, CancellationToken cancellationToken = default);
}

public class VouchService : IVouchService
{
    private readonly JetlagBotDbContext _db;
    private readonly IGuildSettingsService _settings;
    private readonly IClock _clock;

    public VouchService(JetlagBotDbContext db, IGuildSettingsService settings, IClock clock)
    {
        _db = db;
        _settings = settings;
        _clock = clock;
    }

    public async Task<VouchResult> CreateVouchAsync(CreateVouchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TargetUserId == request.VoucherUserId)
        {
            return VouchResult.Failed("Du kan ikke anbefale deg selv.");
        }

        var settings = await _settings.GetOrCreateAsync(request.GuildId, cancellationToken);
        var now = _clock.UtcNow;

        if (request.VoucherJoinedAtUtc is not DateTime joinedAt)
        {
            return VouchResult.Failed(
                "Jeg kunne ikke fastslå hvor lenge du har vært medlem av denne serveren, så jeg kan ikke behandle anbefalingen din akkurat nå.");
        }

        var membershipDays = (now - joinedAt).TotalDays;
        if (membershipDays < settings.MinimumMembershipAgeDays)
        {
            return VouchResult.Failed(
                $"Du må ha vært medlem av denne serveren i minst {settings.MinimumMembershipAgeDays} dager før du kan anbefale noen.");
        }

        var cooldownStart = now - TimeSpan.FromDays(settings.VouchCooldownDays);
        var lastVouch = await _db.Vouches
            .Where(v => v.GuildId == request.GuildId
                && v.VoucherUserId == request.VoucherUserId
                && v.CreatedAtUtc >= cooldownStart)
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastVouch is not null)
        {
            var nextAllowed = lastVouch.CreatedAtUtc.AddDays(settings.VouchCooldownDays);
            return VouchResult.Failed(
                $"Du kan bare gi én anbefaling hver {settings.VouchCooldownDays}. dag. Du kan anbefale igjen etter {nextAllowed:yyyy-MM-dd}.");
        }

        var message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim();

        var vouch = new Vouch
        {
            GuildId = request.GuildId,
            TargetUserId = request.TargetUserId,
            TargetDisplayName = request.TargetDisplayName,
            VoucherUserId = request.VoucherUserId,
            VoucherDisplayName = request.VoucherDisplayName,
            Message = message,
            CreatedAtUtc = now,
        };

        _db.Vouches.Add(vouch);
        await _db.SaveChangesAsync(cancellationToken);

        return VouchResult.Created(vouch);
    }

    public async Task<IReadOnlyList<Vouch>> GetVouchesAsync(ulong guildId, ulong targetUserId, CancellationToken cancellationToken = default)
    {
        return await _db.Vouches
            .Where(v => v.GuildId == guildId && v.TargetUserId == targetUserId)
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Vouch>> GetGuildVouchesAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        return await _db.Vouches
            .Where(v => v.GuildId == guildId)
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteVouchAsync(int vouchId, CancellationToken cancellationToken = default)
    {
        var vouch = await _db.Vouches.FindAsync(new object?[] { vouchId }, cancellationToken);
        if (vouch is null)
        {
            return false;
        }

        _db.Vouches.Remove(vouch);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
