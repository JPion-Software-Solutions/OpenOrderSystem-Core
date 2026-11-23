using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace OpenOrderSystem.Core.Ui;

/// <summary>
/// Hides UI controllers that are not active in the current boot mode from MVC, so they can't be reached
/// or linked to at all. Controllers outside <c>Ui/</c> are left as they are.
/// </summary>
public class UiControllerFeatureProvider(string modeKey) : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        if (!base.IsController(typeInfo))
            return false;

        var ui = UiControllers.All.FirstOrDefault(c => c.Type == typeInfo.AsType());
        return ui is null || ui.IsActiveIn(modeKey);
    }
}
