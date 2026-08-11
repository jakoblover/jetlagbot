using Discord;
using Discord.WebSocket;

namespace JetlagBot.App.Services;

public sealed class DiscordDmSender(
    DiscordSocketClient client,
    ILogger<DiscordDmSender> logger) : IDiscordDmSender
{
    public async Task SendDmAsync(
        ulong discordUserId,
        string text,
        Embed? embed,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await client.GetUserAsync(discordUserId).ConfigureAwait(false)
            ?? await client.Rest.GetUserAsync(discordUserId).ConfigureAwait(false);

        if (user is null)
        {
            throw new InvalidOperationException($"Discord user {discordUserId} was not found.");
        }

        var channel = await user.CreateDMChannelAsync().ConfigureAwait(false);
        await channel.SendMessageAsync(
            text: string.IsNullOrWhiteSpace(text) ? null : text,
            embed: embed).ConfigureAwait(false);

        logger.LogDebug("Sent bonus DM to Discord user {DiscordUserId}.", discordUserId);
    }
}
