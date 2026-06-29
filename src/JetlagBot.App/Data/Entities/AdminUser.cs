namespace JetlagBot.App.Data.Entities;

/// <summary>A Discord user allowed to access the admin panel.</summary>
public class AdminUser
{
    public ulong DiscordUserId { get; set; }

    public string? DisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ulong? AddedByDiscordUserId { get; set; }
}
