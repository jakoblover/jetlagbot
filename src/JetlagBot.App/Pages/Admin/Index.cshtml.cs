using System.ComponentModel.DataAnnotations;
using JetlagBot.App.Data.Entities;
using JetlagBot.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JetlagBot.App.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly IGuildSettingsService _settingsService;

    public IndexModel(IGuildSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<GuildSettings> GuildSettings { get; private set; } = Array.Empty<GuildSettings>();

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Server-ID")]
        public ulong GuildId { get; set; }

        [Range(0, 100000)]
        [Display(Name = "Minimum medlemskapsalder (dager)")]
        public int MinimumMembershipAgeDays { get; set; }

        [Range(0, 100000)]
        [Display(Name = "Karantenetid mellom anbefalinger (dager)")]
        public int VouchCooldownDays { get; set; }
    }

    public async Task OnGetAsync()
    {
        GuildSettings = await _settingsService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid)
        {
            GuildSettings = await _settingsService.GetAllAsync();
            return Page();
        }

        await _settingsService.UpdateAsync(Input.GuildId, Input.MinimumMembershipAgeDays, Input.VouchCooldownDays);
        StatusMessage = $"Innstillinger for server {Input.GuildId} er lagret.";
        return RedirectToPage();
    }
}
