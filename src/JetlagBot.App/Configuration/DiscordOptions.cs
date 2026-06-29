namespace JetlagBot.App.Configuration;

public class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>Bot token used to connect to the Discord gateway.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>OAuth client id used for the admin web login.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret used for the admin web login.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional guild that the bot primarily serves. When set, settings for this
    /// guild are ensured on startup so the admin panel is usable immediately.
    /// </summary>
    public ulong? PrimaryGuildId { get; set; }

    /// <summary>
    /// Optional guild used for instant slash-command registration during development.
    /// When set, commands are registered to this guild instead of globally.
    /// </summary>
    public ulong? DevGuildId { get; set; }
}
