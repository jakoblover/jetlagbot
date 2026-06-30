using Discord;
using Discord.WebSocket;
using JetlagBot.App.Configuration;
using JetlagBot.App.Services;
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
        _client.ButtonExecuted += OnButtonExecutedAsync;
        _client.SelectMenuExecuted += OnSelectMenuExecutedAsync;
        _client.ModalSubmitted += OnModalSubmittedAsync;
        _client.ThreadCreated += OnThreadCreatedAsync;

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
            var noCommands = Array.Empty<ApplicationCommandProperties>();

            if (_options.DevGuildId is ulong devGuildId)
            {
                var guild = _client.GetGuild(devGuildId);
                if (guild is null)
                {
                    _logger.LogWarning("Configured DevGuildId {GuildId} was not found; registering commands globally.", devGuildId);
                    await _client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
                    _logger.LogInformation("Registered {Count} global slash commands.", commands.Length);
                }
                else
                {
                    // Replace the full set for this guild, deleting any stale commands.
                    await guild.BulkOverwriteApplicationCommandAsync(commands);
                    _logger.LogInformation("Registered {Count} slash commands to guild {GuildId}.", commands.Length, devGuildId);

                    // Clear global commands so they don't appear as duplicates alongside the guild commands.
                    await _client.BulkOverwriteGlobalApplicationCommandsAsync(noCommands);
                    _logger.LogInformation("Cleared global slash commands to prevent duplicates.");
                }
            }
            else
            {
                // Global commands are the source of truth.
                await _client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
                _logger.LogInformation("Registered {Count} global slash commands.", commands.Length);

                // Clear any leftover guild-scoped commands that would appear as duplicates.
                foreach (var guild in _client.Guilds)
                {
                    await guild.BulkOverwriteApplicationCommandAsync(noCommands);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register slash commands.");
        }
    }

    private static ApplicationCommandProperties[] BuildCommands()
    {
        var vouch = new SlashCommandBuilder()
            .WithName("vouch")
            .WithDescription("Anbefal et medlem av denne serveren.")
            .AddOption("user", ApplicationCommandOptionType.User, "Brukeren du vil anbefale.", isRequired: true)
            .AddOption("message", ApplicationCommandOptionType.String, "En valgfri melding du vil legge ved anbefalingen.", isRequired: false)
            .Build();

        var vouches = new SlashCommandBuilder()
            .WithName("vouches")
            .WithDescription("Vis privat anbefalingene et medlem har mottatt.")
            .AddOption("user", ApplicationCommandOptionType.User, "Brukeren hvis anbefalinger du vil se.", isRequired: true)
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
                    await command.RespondAsync("Ukjent kommando.", ephemeral: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling slash command {Command}.", command.Data.Name);

            const string error = "Noe gikk galt under behandlingen av kommandoen din. Prøv igjen senere.";
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

    private async Task OnButtonExecutedAsync(SocketMessageComponent component)
    {
        await DispatchComponentAsync(
            (handler) => handler.HandleButtonAsync(component),
            component,
            "button");
    }

    private async Task OnSelectMenuExecutedAsync(SocketMessageComponent component)
    {
        await DispatchComponentAsync(
            (handler) => handler.HandleSelectMenuAsync(component),
            component,
            "select menu");
    }

    private async Task DispatchComponentAsync(
        Func<VouchComponentHandler, Task> action,
        SocketMessageComponent component,
        string kind)
    {
        if (!component.Data.CustomId.StartsWith("vouch_", StringComparison.Ordinal))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<VouchComponentHandler>();

        try
        {
            await action(handler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {Kind} interaction {CustomId}.", kind, component.Data.CustomId);
            await RespondWithErrorAsync(component);
        }
    }

    private async Task OnModalSubmittedAsync(SocketModal modal)
    {
        if (!modal.Data.CustomId.StartsWith("vouch_", StringComparison.Ordinal))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<VouchComponentHandler>();

        try
        {
            await handler.HandleModalAsync(modal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling modal {CustomId}.", modal.Data.CustomId);

            const string error = "Noe gikk galt. Prøv igjen senere.";
            if (modal.HasResponded)
            {
                await modal.FollowupAsync(error, ephemeral: true);
            }
            else
            {
                await modal.RespondAsync(error, ephemeral: true);
            }
        }
    }

    private static async Task RespondWithErrorAsync(SocketMessageComponent component)
    {
        const string error = "Noe gikk galt. Prøv igjen senere.";
        if (component.HasResponded)
        {
            await component.FollowupAsync(error, ephemeral: true);
        }
        else
        {
            await component.RespondAsync(error, ephemeral: true);
        }
    }

    private async Task OnThreadCreatedAsync(SocketThreadChannel thread)
    {
        try
        {
            if (thread.ParentChannel?.Id is not ulong parentId)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<IGuildSettingsService>();
            var settings = await settingsService.GetOrCreateAsync(thread.Guild.Id);

            if (settings.VouchChannelId != parentId)
            {
                return;
            }

            await thread.SendMessageAsync(VouchComponentHandler.PanelText, components: VouchComponentHandler.BuildPanel());
            _logger.LogInformation("Posted vouch panel to thread {ThreadId} in guild {GuildId}.", thread.Id, thread.Guild.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post vouch panel to thread {ThreadId}.", thread.Id);
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
