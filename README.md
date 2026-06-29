# JetlagBot

A Discord bot, written in C# on .NET 10, that lets server members vouch for one another.
It ships with a browser-based administration panel that you sign in to with your Discord
account. Data is stored in PostgreSQL via Entity Framework Core with migrations.

## Features

- **`/vouch [user] [message?]`** — store a vouch for another member, with an optional message.
- **`/vouches [user]`** — privately (ephemeral) list all vouches a member has received, with messages.
- **Restrictions** (all error messages are ephemeral):
  - You cannot vouch until you have been a member of the server for a configurable amount of time
    (default: 365 days).
  - You can only vouch once per configurable cooldown window (default: 30 days).
  - You cannot vouch for yourself.
- **Admin web panel** (Discord login + user-id allowlist):
  - View and edit the per-guild membership-age and cooldown rules.
  - Browse the vouches a user has received.
  - Manage the admin allowlist.

## Project structure

```
JetlagBot.slnx
src/JetlagBot.App/        ASP.NET Core app: hosts the Discord bot + admin web UI
  Configuration/          Strongly-typed options (Discord, Admin, Database)
  Data/                   EF Core entities, DbContext, design-time factory, Migrations
  Services/               Vouch, settings, admin, and clock services
  Discord/                Gateway hosted service + slash-command handler
  Authorization/          Admin allowlist authorization policy
  Pages/                  Razor Pages (home, Account/*, Admin/*)
tests/JetlagBot.Tests/    xUnit tests (vouch rules, admin allowlist)
Dockerfile
docker-compose.yml        App only — no database (see Deployment)
.env.example
```

## Prerequisites

- .NET SDK 10
- A PostgreSQL database (local or hosted)
- A Discord application with a bot, from the [Discord Developer Portal](https://discord.com/developers/applications):
  - Bot token (Bot → Reset Token)
  - OAuth2 client id and client secret (OAuth2 → General)
  - OAuth2 redirect URL set to `https://<your-host>/signin-discord`
    (for local dev: `https://localhost:<port>/signin-discord`)

## Configuration

All settings can be supplied via `appsettings.json` or environment variables. Environment
variables use `__` (double underscore) as the section separator. Secrets should be provided
as environment variables, never committed.

| Setting | Env var | Description |
| --- | --- | --- |
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | PostgreSQL connection string |
| `Database:MigrateOnStartup` | `Database__MigrateOnStartup` | Apply EF migrations on startup (default `true`) |
| `Discord:BotToken` | `Discord__BotToken` | Discord bot token |
| `Discord:ClientId` | `Discord__ClientId` | OAuth client id (admin login) |
| `Discord:ClientSecret` | `Discord__ClientSecret` | OAuth client secret (admin login) |
| `Discord:PrimaryGuildId` | `Discord__PrimaryGuildId` | Optional; ensures settings exist for this guild on startup |
| `Discord:DevGuildId` | `Discord__DevGuildId` | Optional; registers slash commands instantly to one guild (dev) |
| `Admin:DiscordUserIds` | `Admin__DiscordUserIds__0`, `__1`, ... | Bootstrap admin allowlist (Discord user ids) |

Copy `.env.example` to `.env` and fill in the values for Docker / Dokploy.

## Running locally

1. Provide a database connection string and Discord credentials (user secrets or environment variables):

   ```bash
   cd src/JetlagBot.App
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=jetlagbot;Username=postgres;Password=postgres"
   dotnet user-secrets set "Discord:BotToken" "..."
   dotnet user-secrets set "Discord:ClientId" "..."
   dotnet user-secrets set "Discord:ClientSecret" "..."
   dotnet user-secrets set "Admin:DiscordUserIds:0" "<your-discord-user-id>"
   ```

2. Apply migrations (the Development profile sets `Database:MigrateOnStartup=false`, so run them manually):

   ```bash
   dotnet ef database update --project src/JetlagBot.App
   ```

3. Run the app:

   ```bash
   dotnet run --project src/JetlagBot.App
   ```

   Open the printed URL, click **Sign in with Discord**, and (if your id is on the allowlist) you
   reach the admin panel.

> Tip: set `Discord:DevGuildId` to your test server's id so slash commands appear immediately.
> Global command registration can take up to an hour to propagate.

## Database & migrations

```bash
# Add a new migration
dotnet ef migrations add <Name> --project src/JetlagBot.App --output-dir Data/Migrations

# Apply migrations
dotnet ef database update --project src/JetlagBot.App
```

The Discord snowflake ids (guild/user) are stored as text because PostgreSQL has no unsigned
64-bit integer type.

## Build & test

```bash
dotnet build ./JetlagBot.slnx
dotnet test ./JetlagBot.slnx
```

## Deployment (Dokploy)

The database is **not** part of `docker-compose.yml`. Provision PostgreSQL separately in Dokploy
and pass its connection string to the app.

1. Create/attach a PostgreSQL service in Dokploy.
2. Deploy this repository using the provided `Dockerfile` / `docker-compose.yml`.
3. Set the environment variables from the table above (at minimum `ConnectionStrings__Default`,
   `Discord__BotToken`, `Discord__ClientId`, `Discord__ClientSecret`, and at least one
   `Admin__DiscordUserIds__0`).
4. Ensure the Discord OAuth redirect URL matches your public host: `https://<host>/signin-discord`.
5. With `Database__MigrateOnStartup=true` (default), migrations run automatically on startup.

Build and run with Docker Compose locally:

```bash
cp .env.example .env   # then edit values
docker compose --env-file .env up --build
```

The app listens on port `8080` inside the container.
