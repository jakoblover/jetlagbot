using JetlagBot.App.Data;
using JetlagBot.App.Services;
using Microsoft.EntityFrameworkCore;

namespace JetlagBot.Tests;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }
}

internal static class TestDb
{
    public static JetlagBotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JetlagBotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new JetlagBotDbContext(options);
    }
}
