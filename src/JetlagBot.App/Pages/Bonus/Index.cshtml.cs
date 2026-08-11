using JetlagBot.App.Configuration;
using JetlagBot.App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace JetlagBot.App.Pages.Bonus;

[Authorize]
public class IndexModel(
    IBonusAlertService bonusAlertService,
    IOptions<BonusAlertOptions> options) : PageModel
{
    [BindProperty]
    public List<string> SelectedStoreKeys { get; set; } = [];

    [BindProperty]
    public List<string> SelectedStoreNames { get; set; } = [];

    public IReadOnlyList<BonusStoreOption> AvailableStores { get; private set; } = [];

    public IReadOnlyList<BonusStoreOption> CurrentSubscriptions { get; private set; } = [];

    public string? StatusMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool BonusTrackerConfigured =>
        !string.IsNullOrWhiteSpace(options.Value.BonusTrackerBaseUrl);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IBonusAlertService.TryGetDiscordUserId(User, out var discordUserId))
        {
            return Challenge();
        }

        await LoadAsync(discordUserId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!IBonusAlertService.TryGetDiscordUserId(User, out var discordUserId))
        {
            return Challenge();
        }

        try
        {
            var pairs = new List<(string StoreKey, string DisplayName)>();
            for (var index = 0; index < SelectedStoreKeys.Count; index++)
            {
                var key = SelectedStoreKeys[index];
                var name = index < SelectedStoreNames.Count
                    ? SelectedStoreNames[index]
                    : key;
                pairs.Add((key, name));
            }

            await bonusAlertService.ReplaceSubscriptionsAsync(discordUserId, pairs, cancellationToken);
            StatusMessage = "Abonnementene er lagret. Du får DM når valgte butikker får oppdateringer.";
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Kunne ikke lagre abonnementene: {exception.Message}";
        }

        await LoadAsync(discordUserId, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(ulong discordUserId, CancellationToken cancellationToken)
    {
        var subscriptions = await bonusAlertService.GetSubscriptionsAsync(discordUserId, cancellationToken);
        CurrentSubscriptions = subscriptions
            .Select(subscription => new BonusStoreOption
            {
                StoreKey = subscription.StoreKey,
                DisplayName = subscription.StoreDisplayName,
            })
            .ToArray();

        if (SelectedStoreKeys.Count == 0)
        {
            SelectedStoreKeys = CurrentSubscriptions.Select(store => store.StoreKey).ToList();
            SelectedStoreNames = CurrentSubscriptions.Select(store => store.DisplayName).ToList();
        }

        if (BonusTrackerConfigured)
        {
            try
            {
                AvailableStores = await bonusAlertService.SearchSubscribableStoresAsync(null, cancellationToken);
            }
            catch
            {
                AvailableStores = CurrentSubscriptions;
                ErrorMessage ??= "Kunne ikke hente butikkliste fra Bonus Tracker. Viser lagrede abonnementer.";
            }
        }
        else
        {
            AvailableStores = CurrentSubscriptions;
        }
    }
}
