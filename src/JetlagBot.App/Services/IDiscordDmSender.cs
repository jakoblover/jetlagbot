using Discord;

namespace JetlagBot.App.Services;

public interface IDiscordDmSender
{
    Task SendDmAsync(ulong discordUserId, string text, Embed? embed, CancellationToken cancellationToken = default);
}
