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
    private readonly EphemeralResponder _responder;

    public VouchCommandHandler(IVouchService vouchService, EphemeralResponder responder)
    {
        _vouchService = vouchService;
        _responder = responder;
    }

    public async Task HandleVouchAsync(SocketSlashCommand command)
    {
        if (command.GuildId is not ulong guildId)
        {
            await _responder.RespondAsync(command, command.User.Id, "Denne kommandoen kan bare brukes i en server.");
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
            await _responder.RespondAsync(command, command.User.Id, result.ErrorMessage ?? "Noe gikk galt. Prøv igjen senere.");
            return;
        }

        await _responder.RespondAsync(command, command.User.Id, $"Anbefalingen din av {target.Mention} er registrert.");
    }

    public async Task HandleVouchesAsync(SocketSlashCommand command)
    {
        if (command.GuildId is not ulong guildId)
        {
            await _responder.RespondAsync(command, command.User.Id, "Denne kommandoen kan bare brukes i en server.");
            return;
        }

        var target = (IUser)command.Data.Options.First(o => o.Name == "user").Value;
        var targetName = GetDisplayName(target);
        var vouches = await _vouchService.GetVouchesAsync(guildId, target.Id);

        if (vouches.Count == 0)
        {
            await _responder.RespondAsync(command, command.User.Id, $"{targetName} har ingen anbefalinger ennå.");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"**Anbefalinger for {targetName}** ({vouches.Count}):");

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
            builder.AppendLine("_(listen er forkortet; det finnes flere anbefalinger)_");
        }

        await _responder.RespondAsync(command, command.User.Id, builder.ToString());
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
