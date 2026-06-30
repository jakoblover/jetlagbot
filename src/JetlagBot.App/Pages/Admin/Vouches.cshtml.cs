using Discord.WebSocket;
using JetlagBot.App.Data.Entities;
using JetlagBot.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JetlagBot.App.Pages.Admin;

public class VouchesModel : PageModel
{
    private readonly IVouchService _vouchService;
    private readonly DiscordSocketClient _client;

    public VouchesModel(IVouchService vouchService, DiscordSocketClient client)
    {
        _vouchService = vouchService;
        _client = client;
    }

    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? TargetUserId { get; set; }

    public IReadOnlyList<Vouch> Vouches { get; private set; } = Array.Empty<Vouch>();

    public IReadOnlyList<GuildOption> Guilds { get; private set; } = Array.Empty<GuildOption>();

    public bool Searched { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public record GuildOption(ulong Id, string Name);

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var deleted = await _vouchService.DeleteVouchAsync(id);
        StatusMessage = deleted
            ? $"Anbefaling #{id} ble slettet."
            : $"Fant ingen anbefaling med ID {id}.";

        return RedirectToPage(new { GuildId, TargetUserId });
    }

    private async Task LoadAsync()
    {
        Guilds = _client.Guilds
            .Select(g => new GuildOption(g.Id, g.Name))
            .OrderBy(g => g.Name)
            .ToList();

        if (GuildId is not ulong guildId)
        {
            return;
        }

        Searched = true;
        Vouches = TargetUserId is ulong targetUserId
            ? await _vouchService.GetVouchesAsync(guildId, targetUserId)
            : await _vouchService.GetGuildVouchesAsync(guildId);
    }
}
