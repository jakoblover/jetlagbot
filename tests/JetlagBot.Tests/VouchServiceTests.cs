using JetlagBot.App.Data;
using JetlagBot.App.Data.Entities;
using JetlagBot.App.Services;

namespace JetlagBot.Tests;

public class VouchServiceTests
{
    private const ulong GuildId = 111UL;
    private const ulong VoucherId = 222UL;
    private const ulong TargetId = 333UL;

    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static VouchService CreateService(JetlagBotDbContext db, IClock clock)
    {
        var settings = new GuildSettingsService(db);
        return new VouchService(db, settings, clock);
    }

    private static CreateVouchRequest Request(DateTime? joinedAtUtc, string? message = null, ulong voucherId = VoucherId, ulong targetId = TargetId)
        => new(
            GuildId: GuildId,
            TargetUserId: targetId,
            TargetDisplayName: "Target",
            VoucherUserId: voucherId,
            VoucherDisplayName: "Voucher",
            VoucherJoinedAtUtc: joinedAtUtc,
            Message: message);

    [Fact]
    public async Task CreateVouch_Succeeds_ForEligibleMember()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db, new FakeClock(Now));

        var result = await service.CreateVouchAsync(Request(Now.AddYears(-2), "Great person"));

        Assert.True(result.Success);
        Assert.NotNull(result.Vouch);
        Assert.Equal("Great person", result.Vouch!.Message);
        Assert.Single(db.Vouches);
    }

    [Fact]
    public async Task CreateVouch_Fails_WhenMemberTooNew()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db, new FakeClock(Now));

        // Joined 100 days ago, default minimum is 365 days.
        var result = await service.CreateVouchAsync(Request(Now.AddDays(-100)));

        Assert.False(result.Success);
        Assert.Contains("365 dager", result.ErrorMessage);
        Assert.Empty(db.Vouches);
    }

    [Fact]
    public async Task CreateVouch_Fails_WhenJoinDateUnknown()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db, new FakeClock(Now));

        var result = await service.CreateVouchAsync(Request(joinedAtUtc: null));

        Assert.False(result.Success);
        Assert.Empty(db.Vouches);
    }

    [Fact]
    public async Task CreateVouch_Fails_WhenSelfVouching()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db, new FakeClock(Now));

        var result = await service.CreateVouchAsync(Request(Now.AddYears(-2), voucherId: VoucherId, targetId: VoucherId));

        Assert.False(result.Success);
        Assert.Contains("deg selv", result.ErrorMessage);
        Assert.Empty(db.Vouches);
    }

    [Fact]
    public async Task CreateVouch_Fails_WhenWithinCooldown()
    {
        using var db = TestDb.CreateContext();
        var clock = new FakeClock(Now);
        var service = CreateService(db, clock);

        var first = await service.CreateVouchAsync(Request(Now.AddYears(-2)));
        Assert.True(first.Success);

        // 10 days later, default cooldown is 30 days.
        clock.UtcNow = Now.AddDays(10);
        var second = await service.CreateVouchAsync(Request(Now.AddYears(-2), targetId: 444UL));

        Assert.False(second.Success);
        Assert.Contains("hver 30", second.ErrorMessage);
        Assert.Single(db.Vouches);
    }

    [Fact]
    public async Task CreateVouch_Succeeds_AfterCooldownExpires()
    {
        using var db = TestDb.CreateContext();
        var clock = new FakeClock(Now);
        var service = CreateService(db, clock);

        await service.CreateVouchAsync(Request(Now.AddYears(-2)));

        clock.UtcNow = Now.AddDays(31);
        var result = await service.CreateVouchAsync(Request(Now.AddYears(-2), targetId: 444UL));

        Assert.True(result.Success);
        Assert.Equal(2, db.Vouches.Count());
    }

    [Fact]
    public async Task CreateVouch_RespectsConfigurableSettings()
    {
        using var db = TestDb.CreateContext();
        var clock = new FakeClock(Now);
        var settings = new GuildSettingsService(db);
        await settings.UpdateAsync(GuildId, minimumMembershipAgeDays: 30, vouchCooldownDays: 7, vouchChannelId: null);
        var service = new VouchService(db, settings, clock);

        // Joined 60 days ago: below default 365 but above the configured 30.
        var result = await service.CreateVouchAsync(Request(Now.AddDays(-60)));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetVouches_ReturnsNewestFirst()
    {
        using var db = TestDb.CreateContext();
        var clock = new FakeClock(Now);
        var service = CreateService(db, clock);

        await service.CreateVouchAsync(Request(Now.AddYears(-2), "first", voucherId: 1001UL));
        clock.UtcNow = Now.AddDays(40);
        await service.CreateVouchAsync(Request(Now.AddYears(-2), "second", voucherId: 1002UL));

        var vouches = await service.GetVouchesAsync(GuildId, TargetId);

        Assert.Equal(2, vouches.Count);
        Assert.Equal("second", vouches[0].Message);
        Assert.Equal("first", vouches[1].Message);
    }

    [Fact]
    public async Task CreateVouch_TrimsBlankMessageToNull()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db, new FakeClock(Now));

        var result = await service.CreateVouchAsync(Request(Now.AddYears(-2), message: "   "));

        Assert.True(result.Success);
        Assert.Null(result.Vouch!.Message);
    }

    [Fact]
    public async Task GetGuildVouches_ReturnsAllTargets_NewestFirst()
    {
        using var db = TestDb.CreateContext();
        var clock = new FakeClock(Now);
        var service = CreateService(db, clock);

        await service.CreateVouchAsync(Request(Now.AddYears(-2), "for-333", voucherId: 1001UL, targetId: 333UL));
        clock.UtcNow = Now.AddDays(40);
        await service.CreateVouchAsync(Request(Now.AddYears(-2), "for-444", voucherId: 1002UL, targetId: 444UL));

        var vouches = await service.GetGuildVouchesAsync(GuildId);

        Assert.Equal(2, vouches.Count);
        Assert.Equal("for-444", vouches[0].Message);
        Assert.Equal("for-333", vouches[1].Message);
    }

    [Fact]
    public async Task DeleteVouch_RemovesVouch_WhenItExists()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db, new FakeClock(Now));

        var created = await service.CreateVouchAsync(Request(Now.AddYears(-2)));
        var id = created.Vouch!.Id;

        var deleted = await service.DeleteVouchAsync(id);

        Assert.True(deleted);
        Assert.Empty(db.Vouches);
    }

    [Fact]
    public async Task DeleteVouch_ReturnsFalse_WhenMissing()
    {
        using var db = TestDb.CreateContext();
        var service = CreateService(db, new FakeClock(Now));

        var deleted = await service.DeleteVouchAsync(99999);

        Assert.False(deleted);
    }
}
