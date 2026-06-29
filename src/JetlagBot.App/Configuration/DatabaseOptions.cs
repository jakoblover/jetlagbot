namespace JetlagBot.App.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// When true, EF Core migrations are applied automatically on startup.
    /// Disable for local development when no database is available.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = true;
}
