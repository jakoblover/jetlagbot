using System.Globalization;
using System.Text;
using Discord;
using Discord.WebSocket;
using JetlagBot.App.Services;

namespace JetlagBot.App.Discord;

/// <summary>
/// Drives the interactive vouch panel: the public button message posted in threads and the
/// ephemeral button/select/modal interactions it triggers. The two buttons are re-shown after
/// every completed action so a member can keep vouching without scrolling back up.
/// </summary>
public class VouchComponentHandler
{
    public const string GiveButtonId = "vouch_panel_give";
    public const string ShowButtonId = "vouch_panel_show";
    public const string GiveSelectId = "vouch_select_give";
    public const string ShowSelectId = "vouch_select_show";
    public const string GiveModalPrefix = "vouch_modal_give:";
    public const string ModalMessageInputId = "message";

    private const int MaxResponseLength = 1900;

    private readonly IVouchService _vouchService;
    private readonly DiscordSocketClient _client;

    public VouchComponentHandler(IVouchService vouchService, DiscordSocketClient client)
    {
        _vouchService = vouchService;
        _client = client;
    }

    /// <summary>Text of the public panel message posted in a thread.</summary>
    public static string PanelText =>
        "**Anbefalinger**\n" +
        "Bruk knappene under for å anbefale et medlem eller se anbefalingene til et medlem. " +
        "Svarene er private – bare du ser dem.";

    /// <summary>The two-button action row used both for the public panel and the ephemeral re-prompts.</summary>
    public static MessageComponent BuildPanel()
        => new ComponentBuilder()
            .WithButton("Gi anbefaling", GiveButtonId, ButtonStyle.Success)
            .WithButton("Vis anbefalinger", ShowButtonId, ButtonStyle.Primary)
            .Build();

    public async Task HandleButtonAsync(SocketMessageComponent component)
    {
        switch (component.Data.CustomId)
        {
            case GiveButtonId:
                await RespondWithUserSelectAsync(component, GiveSelectId, "Velg hvem du vil anbefale:");
                break;
            case ShowButtonId:
                await RespondWithUserSelectAsync(component, ShowSelectId, "Velg hvem du vil se anbefalinger for:");
                break;
        }
    }

    public async Task HandleSelectMenuAsync(SocketMessageComponent component)
    {
        if (component.GuildId is not ulong guildId)
        {
            await component.RespondAsync("Denne handlingen kan bare brukes i en server.", ephemeral: true);
            return;
        }

        if (!TryGetSelectedUserId(component, out var targetUserId))
        {
            await component.RespondAsync("Ingen bruker ble valgt.", ephemeral: true, components: BuildPanel());
            return;
        }

        switch (component.Data.CustomId)
        {
            case GiveSelectId:
                await ShowGiveModalAsync(component, targetUserId);
                break;
            case ShowSelectId:
                await ShowVouchesAsync(component, guildId, targetUserId);
                break;
        }
    }

    public async Task HandleModalAsync(SocketModal modal)
    {
        if (!modal.Data.CustomId.StartsWith(GiveModalPrefix, StringComparison.Ordinal))
        {
            return;
        }

        if (modal.GuildId is not ulong guildId)
        {
            await modal.RespondAsync("Denne handlingen kan bare brukes i en server.", ephemeral: true);
            return;
        }

        var targetIdText = modal.Data.CustomId[GiveModalPrefix.Length..];
        if (!ulong.TryParse(targetIdText, out var targetUserId))
        {
            await modal.RespondAsync("Noe gikk galt. Prøv igjen.", ephemeral: true, components: BuildPanel());
            return;
        }

        var message = modal.Data.Components
            .FirstOrDefault(c => c.CustomId == ModalMessageInputId)?.Value;

        var guild = _client.GetGuild(guildId);
        var voucher = guild?.GetUser(modal.User.Id) ?? modal.User as SocketGuildUser;
        var joinedAtUtc = voucher?.JoinedAt?.UtcDateTime;

        var request = new CreateVouchRequest(
            GuildId: guildId,
            TargetUserId: targetUserId,
            TargetDisplayName: ResolveDisplayName(guild, targetUserId),
            VoucherUserId: modal.User.Id,
            VoucherDisplayName: ResolveDisplayName(guild, modal.User.Id),
            VoucherJoinedAtUtc: joinedAtUtc,
            Message: message);

        var result = await _vouchService.CreateVouchAsync(request);
        var text = result.Success
            ? $"✅ Anbefalingen din av {MentionUtils.MentionUser(targetUserId)} er registrert."
            : result.ErrorMessage;

        await modal.RespondAsync(text, ephemeral: true, components: BuildPanel());
    }

    private static async Task RespondWithUserSelectAsync(SocketMessageComponent component, string customId, string prompt)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithType(ComponentType.UserSelect)
            .WithPlaceholder("Velg en bruker")
            .WithMinValues(1)
            .WithMaxValues(1);

        var components = new ComponentBuilder().WithSelectMenu(menu).Build();
        await component.RespondAsync(prompt, components: components, ephemeral: true);
    }

    private static async Task ShowGiveModalAsync(SocketMessageComponent component, ulong targetUserId)
    {
        var modal = new ModalBuilder()
            .WithTitle("Gi en anbefaling")
            .WithCustomId($"{GiveModalPrefix}{targetUserId}")
            .AddTextInput(
                "Melding (valgfritt)",
                ModalMessageInputId,
                TextInputStyle.Paragraph,
                required: false,
                maxLength: 2000);

        await component.RespondWithModalAsync(modal.Build());
    }

    private async Task ShowVouchesAsync(SocketMessageComponent component, ulong guildId, ulong targetUserId)
    {
        var guild = _client.GetGuild(guildId);
        var targetName = ResolveDisplayName(guild, targetUserId);
        var vouches = await _vouchService.GetVouchesAsync(guildId, targetUserId);

        if (vouches.Count == 0)
        {
            await component.RespondAsync($"{targetName} har ingen anbefalinger ennå.", ephemeral: true, components: BuildPanel());
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"**Anbefalinger for {targetName}** ({vouches.Count}):");

        foreach (var vouch in vouches)
        {
            var line = $"- **{vouch.VoucherDisplayName}** ({vouch.CreatedAtUtc:yyyy-MM-dd})";
            if (!string.IsNullOrWhiteSpace(vouch.Message))
            {
                line += $": {vouch.Message}";
            }

            if (builder.Length + line.Length + 1 > MaxResponseLength)
            {
                builder.AppendLine("_(listen er forkortet; det finnes flere anbefalinger)_");
                break;
            }

            builder.AppendLine(line);
        }

        await component.RespondAsync(builder.ToString(), ephemeral: true, components: BuildPanel());
    }

    private static bool TryGetSelectedUserId(SocketMessageComponent component, out ulong userId)
    {
        userId = 0;
        var value = component.Data.Values?.FirstOrDefault();
        return value is not null && ulong.TryParse(value, out userId);
    }

    private string ResolveDisplayName(SocketGuild? guild, ulong userId)
    {
        var guildUser = guild?.GetUser(userId);
        if (guildUser is not null)
        {
            return string.IsNullOrWhiteSpace(guildUser.DisplayName) ? guildUser.Username : guildUser.DisplayName;
        }

        var user = _client.GetUser(userId);
        if (user is not null)
        {
            return user.GlobalName ?? user.Username;
        }

        return userId.ToString(CultureInfo.InvariantCulture);
    }
}
