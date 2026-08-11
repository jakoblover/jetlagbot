using System.Security.Claims;
using Discord;
using JetlagBot.App.Configuration;
using JetlagBot.App.Data;
using JetlagBot.App.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JetlagBot.App.Services;

public interface IBonusAlertService
{
    Task<BonusUpdatesResult> ProcessUpdatesAsync(
        BonusUpdatesRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BonusStoreSubscription>> GetSubscriptionsAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    Task ReplaceSubscriptionsAsync(
        ulong discordUserId,
        IReadOnlyList<(string StoreKey, string DisplayName)> stores,
        CancellationToken cancellationToken = default);

    Task<BonusSubscriptionResult> AddSubscriptionAsync(
        ulong discordUserId,
        string storeQueryOrKey,
        CancellationToken cancellationToken = default);

    Task<BonusSubscriptionResult> RemoveSubscriptionAsync(
        ulong discordUserId,
        string storeQueryOrKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BonusStoreOption>> SearchSubscribableStoresAsync(
        string? query,
        CancellationToken cancellationToken = default);

    static bool TryGetDiscordUserId(ClaimsPrincipal user, out ulong discordUserId)
    {
        discordUserId = 0;
        var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return ulong.TryParse(idValue, out discordUserId);
    }
}

public sealed class BonusAlertService(
    JetlagBotDbContext db,
    IDiscordDmSender dmSender,
    IBonusStoreCatalogCache storeCatalog,
    IClock clock,
    IOptions<BonusAlertOptions> options,
    ILogger<BonusAlertService> logger) : IBonusAlertService
{
    public async Task<BonusUpdatesResult> ProcessUpdatesAsync(
        BonusUpdatesRequest request,
        CancellationToken cancellationToken = default)
    {
        var updates = (request.Updates ?? [])
            .Where(update => !string.IsNullOrWhiteSpace(update.StoreName))
            .ToArray();
        if (updates.Length == 0)
        {
            return new BonusUpdatesResult();
        }

        var keysByUpdate = updates
            .Select(update => (Update: update, Keys: MatchKeysFor(update)))
            .ToArray();
        var allKeys = keysByUpdate
            .SelectMany(item => item.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (allKeys.Length == 0)
        {
            return new BonusUpdatesResult { UpdateCount = updates.Length };
        }

        var subscriptions = await db.BonusStoreSubscriptions
            .AsNoTracking()
            .Where(subscription => allKeys.Contains(subscription.StoreKey))
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            logger.LogInformation(
                "Received {UpdateCount} bonus updates; no matching subscriptions.",
                updates.Length);
            return new BonusUpdatesResult { UpdateCount = updates.Length };
        }

        var updatesByUser = new Dictionary<ulong, List<BonusStoreUpdateDto>>();
        foreach (var item in keysByUpdate)
        {
            var matchedUserIds = subscriptions
                .Where(subscription => item.Keys.Contains(subscription.StoreKey, StringComparer.Ordinal))
                .Select(subscription => subscription.DiscordUserId)
                .Distinct()
                .ToArray();

            foreach (var userId in matchedUserIds)
            {
                if (!updatesByUser.TryGetValue(userId, out var list))
                {
                    list = [];
                    updatesByUser[userId] = list;
                }

                list.Add(item.Update);
            }
        }

        var sent = 0;
        var failures = 0;
        foreach (var (userId, userUpdates) in updatesByUser)
        {
            try
            {
                var embed = BuildEmbed(userUpdates);
                await dmSender.SendDmAsync(
                    userId,
                    text: string.Empty,
                    embed: embed,
                    cancellationToken);
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures++;
                logger.LogWarning(
                    exception,
                    "Failed to send bonus DM to Discord user {DiscordUserId} for {UpdateCount} updates.",
                    userId,
                    userUpdates.Count);
            }
        }

        logger.LogInformation(
            "Processed {UpdateCount} bonus updates for {SubscriberCount} subscribers. Sent={Sent} Failures={Failures}.",
            updates.Length,
            updatesByUser.Count,
            sent,
            failures);

        return new BonusUpdatesResult
        {
            UpdateCount = updates.Length,
            SubscriberCount = updatesByUser.Count,
            MessagesSent = sent,
            MessageFailures = failures,
        };
    }

    public async Task<IReadOnlyList<BonusStoreSubscription>> GetSubscriptionsAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        return await db.BonusStoreSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.DiscordUserId == discordUserId)
            .OrderBy(subscription => subscription.StoreDisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceSubscriptionsAsync(
        ulong discordUserId,
        IReadOnlyList<(string StoreKey, string DisplayName)> stores,
        CancellationToken cancellationToken = default)
    {
        var max = Math.Clamp(options.Value.MaxSubscriptionsPerUser, 1, 200);
        var normalized = stores
            .Select(store => (
                StoreKey: store.StoreKey.Trim(),
                DisplayName: string.IsNullOrWhiteSpace(store.DisplayName)
                    ? store.StoreKey.Trim()
                    : store.DisplayName.Trim()))
            .Where(store => !string.IsNullOrWhiteSpace(store.StoreKey))
            .GroupBy(store => store.StoreKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(max)
            .ToArray();

        var existing = await db.BonusStoreSubscriptions
            .Where(subscription => subscription.DiscordUserId == discordUserId)
            .ToListAsync(cancellationToken);

        db.BonusStoreSubscriptions.RemoveRange(existing);

        var now = clock.UtcNow;
        foreach (var store in normalized)
        {
            db.BonusStoreSubscriptions.Add(new BonusStoreSubscription
            {
                DiscordUserId = discordUserId,
                StoreKey = store.StoreKey,
                StoreDisplayName = store.DisplayName,
                CreatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BonusSubscriptionResult> AddSubscriptionAsync(
        ulong discordUserId,
        string storeQueryOrKey,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveStoreOptionAsync(storeQueryOrKey, cancellationToken);
        if (resolved is null)
        {
            return BonusSubscriptionResult.Fail(
                "Fant ingen butikk som matcher. Bruk autocompletion eller skriv et mer presist navn.");
        }

        var existing = await db.BonusStoreSubscriptions
            .FirstOrDefaultAsync(
                subscription =>
                    subscription.DiscordUserId == discordUserId
                    && subscription.StoreKey == resolved.StoreKey,
                cancellationToken);
        if (existing is not null)
        {
            return BonusSubscriptionResult.Ok($"Du abonnerer allerede på **{existing.StoreDisplayName}**.");
        }

        var max = Math.Clamp(options.Value.MaxSubscriptionsPerUser, 1, 200);
        var count = await db.BonusStoreSubscriptions
            .CountAsync(subscription => subscription.DiscordUserId == discordUserId, cancellationToken);
        if (count >= max)
        {
            return BonusSubscriptionResult.Fail(
                $"Du kan abonnere på maks {max} butikker. Fjern en butikk før du legger til en ny.");
        }

        db.BonusStoreSubscriptions.Add(new BonusStoreSubscription
        {
            DiscordUserId = discordUserId,
            StoreKey = resolved.StoreKey,
            StoreDisplayName = resolved.DisplayName,
            CreatedAtUtc = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return BonusSubscriptionResult.Ok(
            $"Du får nå DM når **{resolved.DisplayName}** har kampanje- eller bonusoppdateringer.");
    }

    public async Task<BonusSubscriptionResult> RemoveSubscriptionAsync(
        ulong discordUserId,
        string storeQueryOrKey,
        CancellationToken cancellationToken = default)
    {
        var query = storeQueryOrKey.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return BonusSubscriptionResult.Fail("Oppgi en butikk.");
        }

        var subscriptions = await db.BonusStoreSubscriptions
            .Where(subscription => subscription.DiscordUserId == discordUserId)
            .ToListAsync(cancellationToken);

        var match = subscriptions.FirstOrDefault(subscription =>
            string.Equals(subscription.StoreKey, query, StringComparison.Ordinal)
            || string.Equals(subscription.StoreDisplayName, query, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            match = subscriptions.FirstOrDefault(subscription =>
                subscription.StoreDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (match is null)
        {
            return BonusSubscriptionResult.Fail(
                "Du abonnerer ikke på den butikken. Bruk `/bonus list` for å se abonnementene dine.");
        }

        db.BonusStoreSubscriptions.Remove(match);
        await db.SaveChangesAsync(cancellationToken);
        return BonusSubscriptionResult.Ok($"Fjernet abonnementet på **{match.StoreDisplayName}**.");
    }

    public async Task<IReadOnlyList<BonusStoreOption>> SearchSubscribableStoresAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        // Discord autocomplete has a hard 3s limit. Prefer cache; only briefly try a refresh.
        await storeCatalog
            .RefreshIfNeededAsync(
                maxAge: TimeSpan.FromMinutes(30),
                timeout: TimeSpan.FromMilliseconds(900),
                cancellationToken)
            .ConfigureAwait(false);

        var cached = storeCatalog.Search(query, take: 25);
        if (cached.Count > 0 || storeCatalog.HasData)
        {
            return cached;
        }

        // Cold cache and short refresh failed — one longer attempt (slash subscribe, not autocomplete).
        await storeCatalog
            .RefreshIfNeededAsync(
                maxAge: TimeSpan.Zero,
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken)
            .ConfigureAwait(false);
        return storeCatalog.Search(query, take: 25);
    }

    public static IReadOnlyList<string> MatchKeysFor(BonusStoreUpdateDto update)
    {
        var keys = new List<string>(3);
        if (update.StoreMappingId is Guid mappingId && mappingId != Guid.Empty)
        {
            keys.Add(mappingId.ToString("D"));
        }

        if (update.StoreId is Guid storeId && storeId != Guid.Empty)
        {
            keys.Add(storeId.ToString("D"));
        }

        return keys;
    }

    private async Task<BonusStoreOption?> ResolveStoreOptionAsync(
        string storeQueryOrKey,
        CancellationToken cancellationToken)
    {
        var query = storeQueryOrKey.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        await storeCatalog
            .RefreshIfNeededAsync(
                maxAge: TimeSpan.FromMinutes(30),
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken)
            .ConfigureAwait(false);

        if (Guid.TryParse(query, out _))
        {
            var byKey = storeCatalog.Search(null, take: 25);
            // Search with null only returns first 25 — look up key across a broader filter via name query empty
            // Prefer direct key match from a name-less search of cache: use Search with empty and also try query as key.
            var allMatches = storeCatalog.Search(query, take: 25);
            var exactKey = allMatches.FirstOrDefault(store =>
                string.Equals(store.StoreKey, query, StringComparison.Ordinal));
            if (exactKey is not null)
            {
                return exactKey;
            }

            // Autocomplete value is often the store mapping GUID even when catalog is cold.
            return new BonusStoreOption { StoreKey = query, DisplayName = query };
        }

        var matches = storeCatalog.Search(query, take: 25);
        var exactName = matches.FirstOrDefault(store =>
            string.Equals(store.DisplayName, query, StringComparison.OrdinalIgnoreCase));
        if (exactName is not null)
        {
            return exactName;
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        var startsWith = matches
            .Where(store => store.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (startsWith.Length == 1)
        {
            return startsWith[0];
        }

        return null;
    }

    private static Embed BuildEmbed(IReadOnlyList<BonusStoreUpdateDto> updates)
    {
        var builder = new EmbedBuilder()
            .WithTitle(updates.Count == 1 ? "Bonusoppdatering" : $"{updates.Count} bonusoppdateringer")
            .WithColor(new Color(0x2ecc71))
            .WithTimestamp(DateTimeOffset.UtcNow);

        foreach (var update in updates.Take(10))
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(update.PreviousReward) && !string.IsNullOrWhiteSpace(update.NewReward))
            {
                lines.Add($"{update.PreviousReward} -> {update.NewReward}");
            }
            else if (!string.IsNullOrWhiteSpace(update.BadgeText))
            {
                lines.Add($"Bonus: {update.BadgeText}");
            }

            if (!string.IsNullOrWhiteSpace(update.ProductLabel))
            {
                lines.Add($"Produkt: {update.ProductLabel}");
            }

            if (update.EndsAt is not null)
            {
                lines.Add($"Slutter: {update.EndsAt.Value:yyyy-MM-dd}");
            }

            if (!string.IsNullOrWhiteSpace(update.CashbackUrl))
            {
                lines.Add($"[Åpne bonussiden]({update.CashbackUrl})");
            }

            var name = Truncate($"**{update.StoreName}** ({ProgramLabel(update.Source)})", 256);
            var value = Truncate(
                lines.Count == 0 ? (update.Title ?? "Oppdatering") : string.Join('\n', lines),
                650);
            builder.AddField(name, value, inline: false);
        }

        if (updates.Count > 10)
        {
            builder.WithFooter($"+{updates.Count - 10} flere oppdateringer");
        }

        return builder.Build();
    }

    private static string ProgramLabel(string source)
    {
        return source switch
        {
            "TrumfNetthandel" => "Trumf Netthandel",
            "TrumfFordel" => "Trumf Fordel",
            "SasOnlineShopping" => "SAS Online Shopping",
            _ => source,
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }
}
