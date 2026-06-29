using JetlagBot.App.Configuration;
using JetlagBot.App.Services;
using Microsoft.Extensions.Options;

namespace JetlagBot.Tests;

public class AdminServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AdminService CreateService(JetlagBot.App.Data.JetlagBotDbContext db, params string[] bootstrapIds)
    {
        var options = Options.Create(new AdminOptions { DiscordUserIds = bootstrapIds.ToList() });
        return new AdminService(db, new FakeClock(Now), options);
    }

    [Fact]
    public async Task IsAdmin_TrueForBootstrapId()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db, "555");

        Assert.True(await service.IsAdminAsync(555UL));
        Assert.False(await service.IsAdminAsync(999UL));
    }

    [Fact]
    public async Task AddAndRemoveAdmin_TogglesDatabaseAccess()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db);

        Assert.False(await service.IsAdminAsync(777UL));

        await service.AddAdminAsync(777UL, "Mod", addedByDiscordUserId: 555UL);
        Assert.True(await service.IsAdminAsync(777UL));

        await service.RemoveAdminAsync(777UL);
        Assert.False(await service.IsAdminAsync(777UL));
    }

    [Fact]
    public async Task AddAdmin_IsIdempotent()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db);

        await service.AddAdminAsync(777UL, "Mod", null);
        await service.AddAdminAsync(777UL, "Mod Updated", null);

        var admins = await service.GetAdminsAsync();
        Assert.Single(admins);
        Assert.Equal("Mod Updated", admins[0].DisplayName);
    }
}
