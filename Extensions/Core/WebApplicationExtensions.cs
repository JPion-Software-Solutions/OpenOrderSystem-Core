using System;
using Microsoft.AspNetCore.Identity;
using Serilog;

namespace OpenOrderSystem.Core.Extensions.AspNet;

public static class WebApplicationExtensions
{
    private static readonly string[] _coreUserRoles = [
        "admin",
        "default_admin",
        "manager",
        "terminal_user"
    ];
    public static async Task EnsureCoreUserRolesCreated(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();
        var rolesAdded = new List<string>();

        foreach (var role in _coreUserRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole
                {
                    Name = role,
                    NormalizedName = role.ToUpper()
                });
                rolesAdded.Add(role);
            }
        }

        if (rolesAdded.Any())
            logger.LogInformation($"{rolesAdded.Count} new core roles added: {string.Join(',', rolesAdded)}");
        else
            logger.LogInformation("All core roles present. No new user roles added");
    }
}
