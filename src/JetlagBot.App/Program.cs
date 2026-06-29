using AspNet.Security.OAuth.Discord;
using Discord;
using Discord.WebSocket;
using JetlagBot.App.Authorization;
using JetlagBot.App.Configuration;
using JetlagBot.App.Data;
using JetlagBot.App.Discord;
using JetlagBot.App.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Configuration -------------------------------------------------------------
builder.Services.Configure<DiscordOptions>(builder.Configuration.GetSection(DiscordOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

var discordOptions = builder.Configuration.GetSection(DiscordOptions.SectionName).Get<DiscordOptions>() ?? new DiscordOptions();

// Database ------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Database=jetlagbot;Username=postgres;Password=postgres";

builder.Services.AddDbContext<JetlagBotDbContext>(options => options.UseNpgsql(connectionString));

// Application services ------------------------------------------------------
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IGuildSettingsService, GuildSettingsService>();
builder.Services.AddScoped<IVouchService, VouchService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// Discord bot ---------------------------------------------------------------
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
    AlwaysDownloadUsers = false,
}));
builder.Services.AddScoped<VouchCommandHandler>();
builder.Services.AddHostedService<DiscordBotService>();

// Authentication & authorization -------------------------------------------
var discordLoginConfigured = !string.IsNullOrWhiteSpace(discordOptions.ClientId)
    && !string.IsNullOrWhiteSpace(discordOptions.ClientSecret);

var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        // Fall back to the cookie scheme when Discord login isn't configured so that
        // public pages still work instead of failing OAuth option validation per request.
        options.DefaultChallengeScheme = discordLoginConfigured
            ? DiscordAuthenticationDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

if (discordLoginConfigured)
{
    authBuilder.AddDiscord(options =>
    {
        options.ClientId = discordOptions.ClientId;
        options.ClientSecret = discordOptions.ClientSecret;
        options.CallbackPath = "/signin-discord";
        options.SaveTokens = true;
        options.Scope.Add("identify");
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.AddRequirements(new AdminRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();

// Razor Pages ---------------------------------------------------------------
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});

// Honor X-Forwarded-* headers so the app knows the original https scheme/host when
// running behind a TLS-terminating reverse proxy (e.g. Dokploy / Traefik). This is
// required for Discord OAuth to build correct https callback URLs.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

await InitializeDatabaseAsync(app);

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    if (!databaseOptions.MigrateOnStartup)
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<JetlagBotDbContext>();
    var discord = scope.ServiceProvider.GetRequiredService<IOptions<DiscordOptions>>().Value;

    await db.Database.MigrateAsync();

    // Ensure settings exist for the primary guild so the admin panel is usable immediately.
    if (discord.PrimaryGuildId is ulong primaryGuildId)
    {
        var settingsService = scope.ServiceProvider.GetRequiredService<IGuildSettingsService>();
        await settingsService.GetOrCreateAsync(primaryGuildId);
    }

    logger.LogInformation("Database initialized.");
}

public partial class Program
{
}
