using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;

namespace OpenOrderSystem.Core.Ui;

public static class __Extensions
{
    /// <summary>
    /// Maps a route for every UI controller active in <paramref name="mode"/> (see
    /// <see cref="OosUiActiveModeAttribute"/>). Each controller is served at the URL that mirrors its
    /// folder: <c>Ui/Staff/Dashboard/</c> → <c>/staff/dashboard/{action=Index}/{id?}</c>. Every route is
    /// pinned to its controller; there is no catch-all controller route. The mode's
    /// <see cref="OosUiHomeAttribute"/> controller, if any, also serves its <c>Index</c> at <c>/</c>.
    /// </summary>
    /// <param name="app">The built web application.</param>
    /// <param name="mode">The boot mode being started.</param>
    public static WebApplication MapOosUiRoutes(this WebApplication app, IBootMode mode)
    {
        var modeKey = OpenOrderSystemApplication.GetBootModeKey(mode.GetType());

        // Mapped first so links to the home Index (Url.Action("Index", "Oos")) generate "/".
        if (UiControllers.HomeIn(modeKey) is { } home)
        {
            app.MapControllerRoute(
                name: "ui:home",
                pattern: "",
                defaults: new { controller = home.Name, action = "Index" });
        }

        foreach (var controller in UiControllers.ActiveIn(modeKey))
        {
            var prefix = controller.Folder.ToLowerInvariant();

            app.MapControllerRoute(
                name: $"ui:{prefix}",
                pattern: $"{prefix}/{{action=Index}}/{{id?}}",
                defaults: new { controller = controller.Name });
        }

        return app;
    }
}
