using Microsoft.AspNetCore.Mvc.Controllers;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;
using OpenOrderSystem.Core.Ui;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

public static class UiBuilderExtensions
{
    /// <summary>
    /// Registers MVC for the UI under <c>Ui/</c>. Only the UI controllers active in <paramref name="mode"/>
    /// (see <see cref="OosUiActiveModeAttribute"/>) are registered; the rest are hidden from MVC. Views are
    /// found next to their controller (see <see cref="UiViewLocationExpander"/>), then in <c>Ui/Shared/</c>.
    /// Routes are mapped separately with <see cref="Ui.__Extensions.MapOosUiRoutes"/>.
    /// </summary>
    /// <remarks>Throws at startup if any UI controller is set up wrong (see <see cref="UiControllers.All"/>).</remarks>
    /// <param name="bob">The web application builder.</param>
    /// <param name="mode">The boot mode being started.</param>
    public static WebApplicationBuilder AddOosUi(this WebApplicationBuilder bob, IBootMode mode)
    {
        var modeKey = OpenOrderSystemApplication.GetBootModeKey(mode.GetType());

        // Scan now so setup errors stop startup instead of surfacing on the first request.
        _ = UiControllers.All;

        bob.Services.AddControllersWithViews()
            .ConfigureApplicationPartManager(manager =>
            {
                foreach (var provider in manager.FeatureProviders.OfType<ControllerFeatureProvider>().ToList())
                    manager.FeatureProviders.Remove(provider);

                manager.FeatureProviders.Add(new UiControllerFeatureProvider(modeKey));
            })
            .AddRazorOptions(options =>
            {
                options.ViewLocationFormats.Clear();
                options.ViewLocationFormats.Add("/Ui/Shared/{0}.cshtml");
                options.ViewLocationExpanders.Add(new UiViewLocationExpander());
            });

        return bob;
    }
}
