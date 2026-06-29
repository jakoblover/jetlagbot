using System.Security.Claims;
using JetlagBot.App.Services;
using Microsoft.AspNetCore.Authorization;

namespace JetlagBot.App.Authorization;

/// <summary>
/// Authorizes admin access by checking the authenticated Discord user id against the
/// allowlist (bootstrap configuration plus the database) on every request, so allowlist
/// changes take effect without forcing users to log in again.
/// </summary>
public class AdminAuthorizationHandler : AuthorizationHandler<AdminRequirement>
{
    private readonly IAdminService _adminService;

    public AdminAuthorizationHandler(IAdminService adminService)
    {
        _adminService = adminService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ulong.TryParse(idValue, out var discordUserId)
            && await _adminService.IsAdminAsync(discordUserId))
        {
            context.Succeed(requirement);
        }
    }
}
