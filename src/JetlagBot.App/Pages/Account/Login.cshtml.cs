using AspNet.Security.OAuth.Discord;
using JetlagBot.App.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace JetlagBot.App.Pages.Account;

public class LoginModel : PageModel
{
    private readonly DiscordOptions _discordOptions;

    public LoginModel(IOptions<DiscordOptions> discordOptions)
    {
        _discordOptions = discordOptions.Value;
    }

    public bool DiscordConfigured =>
        !string.IsNullOrWhiteSpace(_discordOptions.ClientId)
        && !string.IsNullOrWhiteSpace(_discordOptions.ClientSecret);

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (!DiscordConfigured)
        {
            // Render the page with a configuration message instead of throwing.
            return Page();
        }

        returnUrl ??= Url.Page("/Admin/Index");
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        return Challenge(properties, DiscordAuthenticationDefaults.AuthenticationScheme);
    }
}
