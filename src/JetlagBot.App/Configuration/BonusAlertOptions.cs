namespace JetlagBot.App.Configuration;

public class BonusAlertOptions
{
    public const string SectionName = "BonusAlert";

    /// <summary>
    /// Shared secret for both directions:
    /// <list type="bullet">
    /// <item>bonus-tracker → JetlagBot: header <c>X-Api-Key</c> on update posts (must match <c>JETLAGBOT_API_KEY</c>)</item>
    /// <item>JetlagBot → bonus-tracker: header <c>X-Api-Key</c> on store search</item>
    /// </list>
    /// When empty, inbound update posts are rejected and authenticated store search is skipped.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Bonus Tracker base URL for store name search.
    /// Prefer the BFF origin (not Cloudflare) e.g. <c>http://bff:8080</c> or the internal BFF host.
    /// Public site origin also works if <c>/api/bff/internal/jetlag/stores</c> is reachable.
    /// </summary>
    public string? BonusTrackerBaseUrl { get; set; }

    /// <summary>
    /// Optional path override for store search.
    /// Default tries authenticated <c>/api/internal/jetlag/stores</c> first, then public unified paths.
    /// </summary>
    public string? BonusTrackerStoresPath { get; set; }

    /// <summary>Maximum store keys a single user may subscribe to.</summary>
    public int MaxSubscriptionsPerUser { get; set; } = 50;
}
