using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels.V2.Core;
using static OpenOrderSystem.Core.Areas.API.V1.System.Maintenance_Bypass.AccessController;

namespace OpenOrderSystem.Core.API.V1.System;

/// <summary>
/// <c>API/V1/System/Maintenance_Bypass</c> endpoints (formerly the API area's AccessController). Used by the order
/// site to issue and check maintenance bypass tokens. Behaves exactly as the <c>[ApiController]</c> it replaced.
/// </summary>
public static class MaintenanceBypassApi
{
    // POST /API/v1/System/Maintenance_Bypass/Issue
    public static async Task<IResult> Issue(
        HttpContext httpContext,
        [FromServices] ApplicationDbContext db,
        [FromServices] UserManager<IdentityUser> userManager,
        [FromServices] SignInManager<IdentityUser> signInManager)
    {
        var binder = new MvcCompatBinder(httpContext);
        var req = (await binder.BodyAsync<BypassLoginRequest>("req"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var user = await userManager.FindByNameAsync(req.Username);
        if (user == null)
            return MvcCompatResults.ClientError(StatusCodes.Status401Unauthorized);

        var pwCheck = await signInManager.CheckPasswordSignInAsync(user, req.Password, false);
        if (!pwCheck.Succeeded)
            return MvcCompatResults.ClientError(StatusCodes.Status401Unauthorized);

        var token = new MaintenanceBypassToken
        {
            Id = Guid.NewGuid().ToString(), // PK and token
            IssuedById = user.Id,
            IssuedBy = user,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = req.Expires
        };

        db.MaintenanceBypassTokens.Add(token);
        await db.SaveChangesAsync();

        return MvcCompatResults.Negotiated(token.Id); // return the token
    }

    // GET /API/v1/System/Maintenance_Bypass/Validate?token=&fromIp=
    public static async Task<IResult> Validate(HttpContext httpContext, [FromServices] ApplicationDbContext db)
    {
        var binder = new MvcCompatBinder(httpContext);
        var token = binder.QueryString("token", required: true)!;
        var fromIp = binder.QueryString("fromIp", required: true)!;
        if (binder.ApiControllerError() is { } error) return error;

        var entity = await db.MaintenanceBypassTokens
            .FirstOrDefaultAsync(t => t.Id == token);

        if (entity == null)
            return MvcCompatResults.ClientError(StatusCodes.Status401Unauthorized);

        if (DateTime.UtcNow > entity.ExpiresAt)
        {
            db.MaintenanceBypassTokens.Remove(entity);
            await db.SaveChangesAsync();
            return MvcCompatResults.ClientError(StatusCodes.Status401Unauthorized);
        }

        entity.LastUsedAt = DateTime.UtcNow;
        entity.LastUsedApiCallerIp = httpContext.Connection.RemoteIpAddress?.ToString();
        entity.LastUsedClientReportedIp = fromIp;
        await db.SaveChangesAsync();

        return Results.Ok();
    }
}
