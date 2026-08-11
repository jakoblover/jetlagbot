using System.Text;
using Discord;
using Discord.WebSocket;
using JetlagBot.App.Services;

namespace JetlagBot.App.Discord;

/// <summary>Handles /bonus subscribe|unsubscribe|list slash commands and store autocomplete.</summary>
public sealed class BonusCommandHandler(
    IBonusAlertService bonusAlertService,
    EphemeralResponder responder)
{
    private const int MaxResponseLength = 1900;
    private const int MaxAutocompleteResults = 25;

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subcommand = command.Data.Options.FirstOrDefault();
        if (subcommand is null)
        {
            await responder.RespondAsync(command, command.User.Id, "Ukjent underkommando.");
            return;
        }

        switch (subcommand.Name)
        {
            case "subscribe":
                await HandleSubscribeAsync(command, subcommand);
                break;
            case "unsubscribe":
                await HandleUnsubscribeAsync(command, subcommand);
                break;
            case "list":
                await HandleListAsync(command);
                break;
            case "help":
                await HandleHelpAsync(command);
                break;
            default:
                await responder.RespondAsync(
                    command,
                    command.User.Id,
                    "Ukjent underkommando. Bruk `/bonus help` for listen over kommandoer.");
                break;
        }
    }

    public async Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        var focused = interaction.Data.Current;
        var query = focused.Value?.ToString() ?? string.Empty;
        var subcommand = interaction.Data.Options.FirstOrDefault()?.Name;

        IReadOnlyList<BonusStoreOption> options;
        if (subcommand == "unsubscribe")
        {
            var subscriptions = await bonusAlertService.GetSubscriptionsAsync(interaction.User.Id);
            options = subscriptions
                .Select(subscription => new BonusStoreOption
                {
                    StoreKey = subscription.StoreKey,
                    DisplayName = subscription.StoreDisplayName,
                })
                .Where(store =>
                    string.IsNullOrWhiteSpace(query)
                    || store.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(store => store.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxAutocompleteResults)
                .ToArray();
        }
        else
        {
            try
            {
                options = (await bonusAlertService.SearchSubscribableStoresAsync(query))
                    .Take(MaxAutocompleteResults)
                    .ToArray();
            }
            catch
            {
                options = [];
            }
        }

        var results = options
            .Select(store => new AutocompleteResult(
                Truncate(store.DisplayName, 100),
                store.StoreKey))
            .ToArray();

        await interaction.RespondAsync(results);
    }

    private async Task HandleSubscribeAsync(SocketSlashCommand command, SocketSlashCommandDataOption subcommand)
    {
        var storeValue = subcommand.Options.FirstOrDefault(option => option.Name == "store")?.Value as string;
        if (string.IsNullOrWhiteSpace(storeValue))
        {
            await responder.RespondAsync(command, command.User.Id, "Oppgi en butikk.");
            return;
        }

        var result = await bonusAlertService.AddSubscriptionAsync(command.User.Id, storeValue);
        await responder.RespondAsync(command, command.User.Id, result.Message);
    }

    private async Task HandleUnsubscribeAsync(SocketSlashCommand command, SocketSlashCommandDataOption subcommand)
    {
        var storeValue = subcommand.Options.FirstOrDefault(option => option.Name == "store")?.Value as string;
        if (string.IsNullOrWhiteSpace(storeValue))
        {
            await responder.RespondAsync(command, command.User.Id, "Oppgi en butikk.");
            return;
        }

        var result = await bonusAlertService.RemoveSubscriptionAsync(command.User.Id, storeValue);
        await responder.RespondAsync(command, command.User.Id, result.Message);
    }

    private async Task HandleListAsync(SocketSlashCommand command)
    {
        var subscriptions = await bonusAlertService.GetSubscriptionsAsync(command.User.Id);
        if (subscriptions.Count == 0)
        {
            await responder.RespondAsync(
                command,
                command.User.Id,
                "Du har ingen butikkabonnementer. Bruk `/bonus subscribe` for å legge til en butikk.");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"**Dine bonusvarsler** ({subscriptions.Count}):");
        foreach (var subscription in subscriptions)
        {
            var line = $"- {subscription.StoreDisplayName}";
            if (builder.Length + line.Length + 1 > MaxResponseLength)
            {
                builder.AppendLine("…");
                break;
            }

            builder.AppendLine(line);
        }

        await responder.RespondAsync(command, command.User.Id, builder.ToString());
    }

    private Task HandleHelpAsync(SocketSlashCommand command)
    {
        const string helpText =
            """
            **Bonusvarsler – kommandoer**

            `/bonus help`
            Vis denne hjelpeteksten.

            `/bonus subscribe store:`
            Abonner på en butikk. Skriv i feltet og velg fra autocompletion. Du får DM når butikken har kampanje eller økt bonus.

            `/bonus unsubscribe store:`
            Fjern abonnement på en butikk. Autocompletion viser butikkene du allerede følger.

            `/bonus list`
            Vis alle butikkene du abonnerer på.

            Du kan også styre abonnementer på nettsiden **Bonusvarsler** (logg inn med Discord).
            """;

        return responder.RespondAsync(command, command.User.Id, helpText.Trim());
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }
}
