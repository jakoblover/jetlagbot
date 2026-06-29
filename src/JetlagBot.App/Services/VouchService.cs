using JetlagBot.App.Data;
using JetlagBot.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetlagBot.App.Services;

public interface IVouchService
{
    Task<VouchResult> CreateVouchAsync(CreateVouchRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vouch>> GetVouchesAsync(ulong guildId, ulong targetUserId, CancellationToken cancellationToken = default);
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
            return VouchResult.Failed("You cannot vouch for yourself.");
        }

        var settings = await _settings.GetOrCreateAsync(request.GuildId, cancellationToken);
        var now = _clock.UtcNow;

        if (request.VoucherJoinedAtUtc is not DateTime joinedAt)
        {
            return VouchResult.Failed(
                "I couldn't determine how long you've been a member of this server, so I can't process your vouch right now.");
        }

        var membershipDays = (now - joinedAt).TotalDays;
        if (membershipDays < settings.MinimumMembershipAgeDays)
        {
            return VouchResult.Failed(
                $"You must have been a member of this server for at least {settings.MinimumMembershipAgeDays} days before you can vouch for someone.");
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
                $"You can only vouch once every {settings.VouchCooldownDays} days. You can vouch again after {nextAllowed:yyyy-MM-dd}.");
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
}
