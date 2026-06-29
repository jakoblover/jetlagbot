using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using JetlagBot.App.Data.Entities;
using JetlagBot.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JetlagBot.App.Pages.Admin;

public class AdminsModel : PageModel
{
    private readonly IAdminService _adminService;

    public AdminsModel(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public IReadOnlyList<AdminUser> Admins { get; private set; } = Array.Empty<AdminUser>();

    public IReadOnlyList<ulong> BootstrapAdminIds { get; private set; } = Array.Empty<ulong>();

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Discord user id")]
        public ulong DiscordUserId { get; set; }

        [Display(Name = "Display name (optional)")]
        [StringLength(256)]
        public string? DisplayName { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var addedBy = GetCurrentUserId();
        await _adminService.AddAdminAsync(Input.DiscordUserId, Input.DisplayName, addedBy);
        StatusMessage = $"Added {Input.DiscordUserId} to the admin allowlist.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(ulong discordUserId)
    {
        await _adminService.RemoveAdminAsync(discordUserId);
        StatusMessage = $"Removed {discordUserId} from the admin allowlist.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Admins = await _adminService.GetAdminsAsync();
        BootstrapAdminIds = _adminService.GetBootstrapAdminIds();
    }

    private ulong? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return ulong.TryParse(idValue, out var id) ? id : null;
    }
}
