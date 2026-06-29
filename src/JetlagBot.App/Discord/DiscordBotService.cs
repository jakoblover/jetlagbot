using Discord;
using Discord.WebSocket;
using JetlagBot.App.Configuration;
using Microsoft.Extensions.Options;

namespace JetlagBot.App.Discord;

/// <summary>
/// Hosts the Discord gateway connection, registers slash commands, and dispatches
/// interactions to <see cref="VouchCommandHandler"/>.
/// </summary>
public class DiscordBotService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordOptions _options;
    private readonly ILogger<DiscordBotService> _logger;

    public DiscordBotService(
        DiscordSocketClient client,
        IServiceScopeFactory scopeFactory,
        IOptions<DiscordOptions> options,
        ILogger<DiscordBotService> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogWarning("Discord bot token is not configured; the Discord bot will not start.");
            return;
        }

        _client.Log += OnLogAsync;
        _client.Ready += OnReadyAsync;
        _client.SlashCommandExecuted += OnSlashCommandExecutedAsync;

        await _client.LoginAsync(TokenType.Bot, _options.BotToken);
        await _client.StartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
        }
    }

    private async Task OnReadyAsync()
    {
        try
        {
            var commands = BuildCommands();

            if (_options.DevGuildId is ulong devGuildId)
            {
                var guild = _client.GetGuild(devGuildId);
                if (guild is null)
                {
                    _logger.LogWarning("Configured DevGuildId {GuildId} was not found; registering commands globally.", devGuildId);
                    await RegisterGlobalAsync(commands);
                }
                else
                {
                    foreach (var command in commands)
                    {
                        await guild.CreateApplicationCommandAsync(command);
                    }

                    _logger.LogInformation("Registered {Count} slash commands to guild {GuildId}.", commands.Length, devGuildId);
                }
            }
            else
            {
                await RegisterGlobalAsync(commands);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register slash commands.");
        }
    }

    private async Task RegisterGlobalAsync(ApplicationCommandProperties[] commands)
    {
        foreach (var command in commands)
        {
            await _client.CreateGlobalApplicationCommandAsync(command);
        }

        _logger.LogInformation("Registered {Count} global slash commands.", commands.Length);
    }

    private static ApplicationCommandProperties[] BuildCommands()
    {
        var vouch = new SlashCommandBuilder()
            .WithName("vouch")
            .WithDescription("Vouch for a member of this server.")
            .AddOption("user", ApplicationCommandOptionType.User, "The user to vouch for.", isRequired: true)
            .AddOption("message", ApplicationCommandOptionType.String, "An optional message to attach to the vouch.", isRequired: false)
            .Build();

        var vouches = new SlashCommandBuilder()
            .WithName("vouches")
            .WithDescription("Privately list the vouches a member has received.")
            .AddOption("user", ApplicationCommandOptionType.User, "The user whose vouches you want to see.", isRequired: true)
            .Build();

        return new ApplicationCommandProperties[] { vouch, vouches };
    }

    private async Task OnSlashCommandExecutedAsync(SocketSlashCommand command)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<VouchCommandHandler>();

        try
        {
            switch (command.Data.Name)
            {
                case "vouch":
                    await handler.HandleVouchAsync(command);
                    break;
                case "vouches":
                    await handler.HandleVouchesAsync(command);
                    break;
                default:
                    await command.RespondAsync("Unknown command.", ephemeral: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling slash command {Command}.", command.Data.Name);

            const string error = "Something went wrong while processing your command. Please try again later.";
            if (command.HasResponded)
            {
                await command.FollowupAsync(error, ephemeral: true);
            }
            else
            {
                await command.RespondAsync(error, ephemeral: true);
            }
        }
    }

    private Task OnLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information,
        };

        _logger.Log(level, message.Exception, "[Discord] {Source}: {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }
}
