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
    /// Bonus Tracker base URL for the <b>protected</b> Jetlag store catalog only.
    /// Public site: <c>https://eb.loever.net</c> → calls <c>/api/bff/internal/jetlag/stores</c> with <see cref="ApiKey"/>.
    /// Direct BFF (same network): <c>http://bff:8080</c> → calls <c>/api/internal/jetlag/stores</c>.
    /// Does not use the public <c>/stores/unified</c> website endpoint.
    /// </summary>
    public string? BonusTrackerBaseUrl { get; set; }

    /// <summary>
    /// Optional path override for the protected store catalog (must require <c>X-Api-Key</c> on the BFF).
    /// Default: <c>/api/bff/internal/jetlag/stores</c> (public frontend) or <c>/api/internal/jetlag/stores</c> (BFF).
    /// </summary>
    public string? BonusTrackerStoresPath { get; set; }

    /// <summary>Maximum store keys a single user may subscribe to.</summary>
    public int MaxSubscriptionsPerUser { get; set; } = 50;
}
