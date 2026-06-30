using JetlagBot.App.Data.Entities;
using JetlagBot.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JetlagBot.App.Pages.Admin;

public class VouchesModel : PageModel
{
    private readonly IVouchService _vouchService;

    public VouchesModel(IVouchService vouchService)
    {
        _vouchService = vouchService;
    }

    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? TargetUserId { get; set; }

    public IReadOnlyList<Vouch> Vouches { get; private set; } = Array.Empty<Vouch>();

    public bool Searched { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

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
