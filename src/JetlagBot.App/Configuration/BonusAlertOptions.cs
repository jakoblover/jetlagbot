namespace JetlagBot.App.Configuration;

public class BonusAlertOptions
{
    public const string SectionName = "BonusAlert";

    /// <summary>
    /// Shared secret that bonus-tracker must send as <c>X-Api-Key</c> on update posts.
    /// When empty, the internal update endpoint rejects all requests.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional public base URL of bonus-tracker (e.g. https://eb.loever.net).
    /// Used by the subscription page to load unified stores for multi-select.
    /// </summary>
    public string? BonusTrackerBaseUrl { get; set; }

    /// <summary>Maximum store keys a single user may subscribe to.</summary>
    public int MaxSubscriptionsPerUser { get; set; } = 50;
}
