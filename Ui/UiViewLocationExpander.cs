using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;

namespace OpenOrderSystem.Core.Ui;

/// <summary>
/// Finds views for UI controllers in the controller's own folder, then in each parent folder's
/// <c>Shared/</c>, then in <c>Ui/Shared/</c> (the configured view location format).
/// </summary>
/// <example>
/// For a controller in <c>Ui/Staff/Dashboard/</c>, <c>View("Index")</c> searches:
/// <c>/Ui/Staff/Dashboard/Index.cshtml</c>, <c>/Ui/Staff/Shared/Index.cshtml</c>, <c>/Ui/Shared/Index.cshtml</c>.
/// </example>
public class UiViewLocationExpander : IViewLocationExpander
{
    private const string FolderKey = "oos-ui-folder";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        // The folder is part of the view location cache key, so two controllers with the same name in
        // different folders don't share cached lookups.
        if (context.ActionContext.ActionDescriptor is ControllerActionDescriptor action &&
            UiFolder.TryGet(action.ControllerTypeInfo, out var folder))
        {
            context.Values[FolderKey] = folder;
        }
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        if (!context.Values.TryGetValue(FolderKey, out var folder) || folder is null)
            return viewLocations;

        return FolderLocations(folder).Concat(viewLocations);
    }

    private static IEnumerable<string> FolderLocations(string folder)
    {
        yield return $"/Ui/{folder}/{{0}}.cshtml";

        var parts = folder.Split('/');
        for (var i = parts.Length - 1; i > 0; i--)
            yield return $"/Ui/{string.Join('/', parts[..i])}/Shared/{{0}}.cshtml";
    }
}
