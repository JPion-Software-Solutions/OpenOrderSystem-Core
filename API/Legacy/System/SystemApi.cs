using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Extensions;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Legacy;

namespace OpenOrderSystem.Core.API.Legacy.System;

/// <summary>Legacy <c>api/legacy/System</c> endpoints (formerly SystemController).</summary>
public static class SystemApi
{
    public static string Version { get; set; } = string.Empty;
    public static DateTime SystemBoot { get; set; }

    // Mapped to /api/system/ping, /ping and /api/legacy/System/Ping for any HTTP method.
    public static IResult Ping()
    {
        return Results.Redirect("/api/v2/system/ping");
    }

    // Any HTTP method.
    public static async Task<IResult> AcceptingOrders(
        [FromServices] StaffTerminalMonitoringService staffTMS,
        [FromServices] ConfigurationService configService,
        [FromServices] ITimeNormalizationService timeService)
    {
        var currentLocalTime = await timeService.GetLocalTime();
        var withinBusinessHours = configService.Settings.IsAcceptingOrders(currentLocalTime);
        var acceptingOrders = staffTMS.TerminalActive && withinBusinessHours;

        var response = new
        {
            acceptingOrders,
            reason = acceptingOrders ? "N/A" :
                !withinBusinessHours ? "The kitchen is currently closed." :
                "Staff terminal unavailable to receive orders.",
            terminalOnline = staffTMS.TerminalActive,
            withinBusinessHours
        };

        if (acceptingOrders)
            return Results.Ok(response);
        else
            return Results.Json(response, statusCode: 503);
    }
}
