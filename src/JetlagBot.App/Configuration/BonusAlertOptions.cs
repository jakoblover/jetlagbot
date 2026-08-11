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
    /// Public base URL of bonus-tracker website or BFF
    /// (e.g. https://eb.loever.net or https://bff.internal:8080).
    /// Used for Discord autocomplete and the web multi-select store list.
    /// </summary>
    public string? BonusTrackerBaseUrl { get; set; }

    /// <summary>
    /// Optional path to unified stores on the base URL.
    /// Default tries <c>/api/bff/stores/unified</c> (public site) then <c>/api/stores/unified</c> (BFF).
    /// </summary>
    public string? BonusTrackerStoresPath { get; set; }

    /// <summary>Maximum store keys a single user may subscribe to.</summary>
    public int MaxSubscriptionsPerUser { get; set; } = 50;
}
