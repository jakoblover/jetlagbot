namespace JetlagBot.App.Data.Entities;

/// <summary>
/// A Discord user subscription to bonus updates for one unified store mapping
/// (or a single source store when no mapping exists).
/// </summary>
public class BonusStoreSubscription
{
    public int Id { get; set; }

    public ulong DiscordUserId { get; set; }

    /// <summary>
    /// Stable key from bonus-tracker: store mapping id, or source store id.
    /// Matched against <c>storeMappingId</c> / <c>storeId</c> on inbound update posts.
    /// </summary>
    public string StoreKey { get; set; } = string.Empty;

    public string StoreDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
