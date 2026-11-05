using OpenOrderSystem.Core.API.V1.System;

namespace OpenOrderSystem.Core.API.V1;

public static class __Extensions
{
    public static WebApplication MapV1ApiRoutes(this WebApplication app)
    {
        var bypass = app.MapGroup("/API/V1/System/Maintenance_Bypass").DisableCookieRedirect();
        bypass.MapPost("/Issue", MaintenanceBypassApi.Issue);
        bypass.MapGet("/Validate", MaintenanceBypassApi.Validate);

        return app;
    }
}
