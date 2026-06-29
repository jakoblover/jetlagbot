using System.Text;
using Discord;
using Discord.WebSocket;
using JetlagBot.App.Services;

namespace JetlagBot.App.Discord;

/// <summary>Handles the /vouch and /vouches slash commands. Scoped per interaction.</summary>
public class VouchCommandHandler
{
    private const int MaxResponseLength = 1900;

    private readonly IVouchService _vouchService;

    public VouchCommandHandler(IVouchService vouchService)
    {
        _vouchService = vouchService;
    }

    public async Task HandleVouchAsync(SocketSlashCommand command)
    {
        if (command.GuildId is not ulong guildId)
        {
            await command.RespondAsync("This command can only be used in a server.", ephemeral: true);
            return;
        }

        var target = (IUser)command.Data.Options.First(o => o.Name == "user").Value;
        var message = command.Data.Options.FirstOrDefault(o => o.Name == "message")?.Value as string;

        DateTime? joinedAtUtc = (command.User as SocketGuildUser)?.JoinedAt?.UtcDateTime;

        var request = new CreateVouchRequest(
            GuildId: guildId,
            TargetUserId: target.Id,
            TargetDisplayName: GetDisplayName(target),
            VoucherUserId: command.User.Id,
            VoucherDisplayName: GetDisplayName(command.User),
            VoucherJoinedAtUtc: joinedAtUtc,
            Message: message);

        var result = await _vouchService.CreateVouchAsync(request);

        if (!result.Success)
        {
            await command.RespondAsync(result.ErrorMessage, ephemeral: true);
            return;
        }

        await command.RespondAsync($"Your vouch for {target.Mention} has been recorded.", ephemeral: true);
    }

    public async Task HandleVouchesAsync(SocketSlashCommand command)
    {
        if (command.GuildId is not ulong guildId)
        {
            await command.RespondAsync("This command can only be used in a server.", ephemeral: true);
            return;
        }

        var target = (IUser)command.Data.Options.First(o => o.Name == "user").Value;
        var targetName = GetDisplayName(target);
        var vouches = await _vouchService.GetVouchesAsync(guildId, target.Id);

        if (vouches.Count == 0)
        {
            await command.RespondAsync($"{targetName} has no vouches yet.", ephemeral: true);
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"**Vouches for {targetName}** ({vouches.Count}):");

        var truncated = false;
        foreach (var vouch in vouches)
        {
            var line = $"- **{vouch.VoucherDisplayName}** ({vouch.CreatedAtUtc:yyyy-MM-dd})";
            if (!string.IsNullOrWhiteSpace(vouch.Message))
            {
                line += $": {vouch.Message}";
            }

            if (builder.Length + line.Length + 1 > MaxResponseLength)
            {
                truncated = true;
                break;
            }

            builder.AppendLine(line);
        }

        if (truncated)
        {
            builder.AppendLine("_(list truncated; more vouches exist)_");
        }

        await command.RespondAsync(builder.ToString(), ephemeral: true);
    }

    private static string GetDisplayName(IUser user)
    {
        if (user is SocketGuildUser guildUser && !string.IsNullOrWhiteSpace(guildUser.DisplayName))
        {
            return guildUser.DisplayName;
        }

        return user.GlobalName ?? user.Username;
    }
}
