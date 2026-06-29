namespace JetlagBot.App.Data.Entities;

/// <summary>A vouch given by one guild member to another.</summary>
public class Vouch
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public ulong TargetUserId { get; set; }

    public string TargetDisplayName { get; set; } = string.Empty;

    public ulong VoucherUserId { get; set; }

    public string VoucherDisplayName { get; set; } = string.Empty;

    public string? Message { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
