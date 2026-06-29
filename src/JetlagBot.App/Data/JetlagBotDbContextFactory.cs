using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace JetlagBot.App.Data;

/// <summary>
/// Enables the EF Core command-line tools to create a DbContext without booting the
/// full web host (which would also start the Discord bot).
/// </summary>
public class JetlagBotDbContextFactory : IDesignTimeDbContextFactory<JetlagBotDbContext>
{
    public JetlagBotDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Database=jetlagbot;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<JetlagBotDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new JetlagBotDbContext(options);
    }
}
