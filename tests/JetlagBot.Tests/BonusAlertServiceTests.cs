using JetlagBot.App.Configuration;
using JetlagBot.App.Data.Entities;
using JetlagBot.App.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Discord;

namespace JetlagBot.Tests;

public class BonusAlertServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessUpdates_SendsDmOnlyToMatchingSubscribers()
    {
        await using var db = TestDb.CreateContext();
        var mappingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var otherMappingId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        db.BonusStoreSubscriptions.AddRange(
            new BonusStoreSubscription
            {
                DiscordUserId = 1001,
                StoreKey = mappingId.ToString("D"),
                StoreDisplayName = "Elkjøp",
                CreatedAtUtc = Now,
            },
            new BonusStoreSubscription
            {
                DiscordUserId = 1002,
                StoreKey = otherMappingId.ToString("D"),
                StoreDisplayName = "Other",
                CreatedAtUtc = Now,
            });
        await db.SaveChangesAsync();

        var dm = new CapturingDmSender();
        var service = CreateService(db, dm);

        var result = await service.ProcessUpdatesAsync(new BonusUpdatesRequest
        {
            Updates =
            [
                new BonusStoreUpdateDto
                {
                    Type = "CampaignPublished",
                    Source = "TrumfNetthandel",
                    StoreName = "Elkjøp",
                    StoreMappingId = mappingId,
                    StoreId = Guid.NewGuid(),
                    BadgeText = "10 %",
                    CashbackUrl = "https://example.test/elkjop",
                    EndsAt = Now.AddDays(3),
                },
            ],
        });

        Assert.Equal(1, result.UpdateCount);
        Assert.Equal(1, result.SubscriberCount);
        Assert.Equal(1, result.MessagesSent);
        Assert.Equal(0, result.MessageFailures);
        Assert.Single(dm.SentUserIds);
        Assert.Equal(1001UL, dm.SentUserIds[0]);
    }

    [Fact]
    public async Task ProcessUpdates_NoSubscriptions_SendsNothing()
    {
        await using var db = TestDb.CreateContext();
        var dm = new CapturingDmSender();
        var service = CreateService(db, dm);

        var result = await service.ProcessUpdatesAsync(new BonusUpdatesRequest
        {
            Updates =
            [
                new BonusStoreUpdateDto
                {
                    Type = "ElevatedBonus",
                    Source = "SasOnlineShopping",
                    StoreName = "Unknown",
                    StoreId = Guid.NewGuid(),
                    PreviousReward = "1 %",
                    NewReward = "3 %",
                    CashbackUrl = "https://example.test/x",
                },
            ],
        });

        Assert.Equal(1, result.UpdateCount);
        Assert.Equal(0, result.SubscriberCount);
        Assert.Equal(0, result.MessagesSent);
        Assert.Empty(dm.SentUserIds);
    }

    [Fact]
    public async Task ReplaceSubscriptions_ReplacesExistingSet()
    {
        await using var db = TestDb.CreateContext();
        db.BonusStoreSubscriptions.Add(new BonusStoreSubscription
        {
            DiscordUserId = 7,
            StoreKey = "old",
            StoreDisplayName = "Old",
            CreatedAtUtc = Now,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new CapturingDmSender());
        await service.ReplaceSubscriptionsAsync(
            7,
            [("aaa", "Store A"), ("bbb", "Store B")]);

        var saved = await service.GetSubscriptionsAsync(7);
        Assert.Equal(2, saved.Count);
        Assert.DoesNotContain(saved, item => item.StoreKey == "old");
        Assert.Contains(saved, item => item.StoreKey == "aaa" && item.StoreDisplayName == "Store A");
    }

    [Fact]
    public async Task AddSubscription_AddsByStoreKey()
    {
        await using var db = TestDb.CreateContext();
        var service = CreateService(db, new CapturingDmSender());
        var key = Guid.NewGuid().ToString("D");

        var result = await service.AddSubscriptionAsync(42, key);

        Assert.True(result.Success);
        var saved = await service.GetSubscriptionsAsync(42);
        Assert.Single(saved);
        Assert.Equal(key, saved[0].StoreKey);
    }

    [Fact]
    public async Task RemoveSubscription_RemovesByDisplayName()
    {
        await using var db = TestDb.CreateContext();
        db.BonusStoreSubscriptions.Add(new BonusStoreSubscription
        {
            DiscordUserId = 42,
            StoreKey = "mapping-1",
            StoreDisplayName = "Elkjøp",
            CreatedAtUtc = Now,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new CapturingDmSender());
        var result = await service.RemoveSubscriptionAsync(42, "elkj");

        Assert.True(result.Success);
        Assert.Empty(await service.GetSubscriptionsAsync(42));
    }

    private static BonusAlertService CreateService(
        JetlagBot.App.Data.JetlagBotDbContext db,
        IDiscordDmSender dmSender)
    {
        var options = Options.Create(new BonusAlertOptions
        {
            ApiKey = "test-key",
            MaxSubscriptionsPerUser = 50,
        });
        return new BonusAlertService(
            db,
            dmSender,
            new EmptyStoreCatalog(),
            new FakeClock(Now),
            options,
            NullLogger<BonusAlertService>.Instance);
    }

    private sealed class EmptyStoreCatalog : IBonusStoreCatalogCache
    {
        public bool HasData => false;

        public IReadOnlyList<BonusStoreOption> Search(string? query, int take = 25) => [];

        public Task RefreshIfNeededAsync(TimeSpan maxAge, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CapturingDmSender : IDiscordDmSender
    {
        public List<ulong> SentUserIds { get; } = [];

        public Task SendDmAsync(ulong discordUserId, string text, Embed? embed, CancellationToken cancellationToken = default)
        {
            SentUserIds.Add(discordUserId);
            return Task.CompletedTask;
        }
    }
}
