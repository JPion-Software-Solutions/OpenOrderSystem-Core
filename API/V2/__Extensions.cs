using OpenOrderSystem.Core.API.V2.System;

namespace OpenOrderSystem.Core.API.V2;

public static class __Extensions
{
    public static WebApplication MapV2ApiRoutes(this WebApplication app, string[]? excluded = null)
    {
        // Ping route is always included.
        app.MapGet("/api/v2/system/ping", PingUtility.Get);
        
        
        
        return app;
    }
}