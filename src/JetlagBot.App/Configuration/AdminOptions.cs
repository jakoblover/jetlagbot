namespace JetlagBot.App.Configuration;

public class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// Discord user ids that bootstrap the admin allowlist. These users always have
    /// access to the admin panel in addition to any admins stored in the database.
    /// </summary>
    public List<string> DiscordUserIds { get; set; } = new();
}
