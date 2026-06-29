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

    public async Task OnGetAsync()
    {
        if (GuildId is ulong guildId && TargetUserId is ulong targetUserId)
        {
            Searched = true;
            Vouches = await _vouchService.GetVouchesAsync(guildId, targetUserId);
        }
    }
}
