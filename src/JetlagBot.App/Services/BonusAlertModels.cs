using System.Text.Json.Serialization;

namespace JetlagBot.App.Services;

public sealed class BonusUpdatesRequest
{
    public List<BonusStoreUpdateDto> Updates { get; set; } = [];
}

public sealed class BonusStoreUpdateDto
{
    /// <summary><c>CampaignPublished</c> or <c>ElevatedBonus</c>.</summary>
    public string Type { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public Guid? StoreId { get; set; }

    public Guid? StoreMappingId { get; set; }

    public string? Title { get; set; }

    public string? BadgeText { get; set; }

    public string CashbackUrl { get; set; } = string.Empty;

    public string? PreviousReward { get; set; }

    public string? NewReward { get; set; }

    public string? ProductLabel { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public DateTimeOffset? ObservedAt { get; set; }
}

public sealed class BonusUpdatesResult
{
    public int UpdateCount { get; init; }

    public int SubscriberCount { get; init; }

    public int MessagesSent { get; init; }

    public int MessageFailures { get; init; }
}

public sealed class BonusStoreOption
{
    public string StoreKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class ReplaceSubscriptionsRequest
{
    public List<string> StoreKeys { get; set; } = [];

    /// <summary>Optional display names aligned by index with <see cref="StoreKeys"/>.</summary>
    public List<string>? StoreDisplayNames { get; set; }
}

public sealed class BonusSubscriptionResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static BonusSubscriptionResult Ok(string message) => new() { Success = true, Message = message };

    public static BonusSubscriptionResult Fail(string message) => new() { Success = false, Message = message };
}
