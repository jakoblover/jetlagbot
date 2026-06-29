using JetlagBot.App.Data.Entities;

namespace JetlagBot.App.Services;

/// <summary>Input for creating a vouch.</summary>
public record CreateVouchRequest(
    ulong GuildId,
    ulong TargetUserId,
    string TargetDisplayName,
    ulong VoucherUserId,
    string VoucherDisplayName,
    DateTime? VoucherJoinedAtUtc,
    string? Message);

/// <summary>Result of attempting to create a vouch.</summary>
public record VouchResult(bool Success, string? ErrorMessage, Vouch? Vouch)
{
    public static VouchResult Failed(string error) => new(false, error, null);

    public static VouchResult Created(Vouch vouch) => new(true, null, vouch);
}
