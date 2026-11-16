using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenOrderSystem.Core.Data.DataModels.V2.Devices;
using OpenOrderSystem.Core.Services.Devices.Interfaces;

namespace OpenOrderSystem.Core.Devices.V1;

public static class UniversalDeviceRoutes
{
    public record EnrollModel(string Username, string Password, string Key, string Name, DeviceFlags Flags,
        IReadOnlyDictionary<string, JsonElement>? Configs);

    public static async Task<IResult> PostEnrollDevice(
        [FromBody] EnrollModel model, 
        [FromServices] SignInManager<IdentityUser> signInManager, 
        [FromServices] UserManager<IdentityUser> userManager,
        [FromServices] IDeviceManager deviceManager)
    {
        var user = await userManager.FindByNameAsync(model.Username);
        if (user == null) return Results.Unauthorized();
        
        var validPass = await signInManager.CheckPasswordSignInAsync(user, model.Password, true);
        if (!validPass.Succeeded) return Results.Unauthorized();

        var canEnroll =
            (await userManager.GetClaimsAsync(user)).FirstOrDefault(c => c.Type == "perm:oos.devices/enroll") != null;
        if (!canEnroll) return Results.Unauthorized();

        var result = await deviceManager.EnrollDeviceAsync(model.Key, model.Name, model.Flags,
            model.Configs ?? new Dictionary<string, JsonElement>());

        if (!result.Success)
            return Results.BadRequest();
        
        return Results.Ok();
    }
}