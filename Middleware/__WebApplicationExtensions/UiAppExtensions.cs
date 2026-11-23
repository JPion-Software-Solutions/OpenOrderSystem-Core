using Microsoft.Extensions.FileProviders;

namespace OpenOrderSystem.Core.Middleware.__WebApplicationExtensions;

public static class UiAppExtensions
{
    /// <summary>
    /// Serves the UI's static files (<c>Ui/static</c>, copied to <c>&lt;output&gt;/static</c> on build) at the
    /// site root, so <c>~/css/base.css</c> resolves to <c>static/css/base.css</c>. Also adds the folder to
    /// the web root file provider so <c>asp-append-version</c> works for UI files. Call it before routing.
    /// </summary>
    /// <param name="app">The built web application.</param>
    public static WebApplication UseOosUiStaticFiles(this WebApplication app)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "static");

        if (!Directory.Exists(root))
        {
            app.Logger.LogWarning("UI static files folder {Root} was not found; UI assets will not be served.", root);
            return app;
        }

        var provider = new PhysicalFileProvider(root);
        app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });

        // asp-append-version hashes files found through the web root provider only; add the UI folder to it.
        app.Environment.WebRootFileProvider = new CompositeFileProvider(provider, app.Environment.WebRootFileProvider);

        return app;
    }
}
