namespace JetlagBot.App.Data.Entities;

/// <summary>Per-guild, admin-configurable vouch rules.</summary>
public class GuildSettings
{
    public const int DefaultMinimumMembershipAgeDays = 365;
    public const int DefaultVouchCooldownDays = 30;

    public ulong GuildId { get; set; }

    /// <summary>
    /// Minimum number of days a member must have been in the guild before they can vouch.
    /// </summary>
    public int MinimumMembershipAgeDays { get; set; } = DefaultMinimumMembershipAgeDays;

    /// <summary>Minimum number of days between vouches by the same member.</summary>
    public int VouchCooldownDays { get; set; } = DefaultVouchCooldownDays;
}
