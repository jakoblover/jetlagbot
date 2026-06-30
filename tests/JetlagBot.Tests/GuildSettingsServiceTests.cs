using JetlagBot.App.Data.Entities;
using JetlagBot.App.Services;

namespace JetlagBot.Tests;

public class GuildSettingsServiceTests
{
    private const ulong GuildId = 555UL;

    [Fact]
    public async Task GetOrCreate_ReturnsDefaults_ForNewGuild()
    {
        using var db = TestDb.CreateContext();
        var service = new GuildSettingsService(db);

        var settings = await service.GetOrCreateAsync(GuildId);

        Assert.Equal(GuildSettings.DefaultMinimumMembershipAgeDays, settings.MinimumMembershipAgeDays);
        Assert.Equal(GuildSettings.DefaultVouchCooldownDays, settings.VouchCooldownDays);
        Assert.Null(settings.VouchChannelId);
    }

    [Fact]
    public async Task Update_PersistsVouchChannelId()
    {
        using var db = TestDb.CreateContext();
        var service = new GuildSettingsService(db);

        await service.UpdateAsync(GuildId, minimumMembershipAgeDays: 100, vouchCooldownDays: 14, vouchChannelId: 999UL);

        var settings = await service.GetOrCreateAsync(GuildId);
        Assert.Equal(100, settings.MinimumMembershipAgeDays);
        Assert.Equal(14, settings.VouchCooldownDays);
        Assert.Equal(999UL, settings.VouchChannelId);
    }

    [Fact]
    public async Task Update_CanClearVouchChannelId()
    {
        using var db = TestDb.CreateContext();
        var service = new GuildSettingsService(db);

        await service.UpdateAsync(GuildId, minimumMembershipAgeDays: 30, vouchCooldownDays: 7, vouchChannelId: 999UL);
        await service.UpdateAsync(GuildId, minimumMembershipAgeDays: 30, vouchCooldownDays: 7, vouchChannelId: null);

        var settings = await service.GetOrCreateAsync(GuildId);
        Assert.Null(settings.VouchChannelId);
    }
}
