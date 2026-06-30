using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;

namespace JetlagBot.App.Discord;

/// <summary>
/// Sends ephemeral interaction responses while keeping only a single ephemeral message
/// visible per user: when a user triggers a new ephemeral response (a slash command, button,
/// select menu or modal submit), the previous one is deleted.
///
/// Discord only allows deleting an interaction's response via its own token, and only for the
/// 15-minute lifetime of that token, so the cleanup is best-effort. This is registered as a
/// singleton so the per-user state is shared across all interaction handlers.
/// </summary>
public class EphemeralResponder
{
    private readonly ConcurrentDictionary<ulong, IDiscordInteraction> _lastByUser = new();

    public async Task RespondAsync(
        SocketInteraction interaction,
        ulong userId,
        string text,
        MessageComponent? components = null)
    {
        await interaction.RespondAsync(text, ephemeral: true, components: components);
        await ReplacePreviousAsync(userId, interaction);
    }

    private async Task ReplacePreviousAsync(ulong userId, IDiscordInteraction current)
    {
        _lastByUser.TryGetValue(userId, out var previous);
        _lastByUser[userId] = current;

        if (previous is null || ReferenceEquals(previous, current))
        {
            return;
        }

        try
        {
            await previous.DeleteOriginalResponseAsync();
        }
        catch
        {
            // The previous ephemeral message may already be gone, or its interaction token
            // may have expired (older than 15 minutes); nothing more we can do.
        }
    }
}
