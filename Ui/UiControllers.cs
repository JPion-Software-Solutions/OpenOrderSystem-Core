using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using OpenOrderSystem.Core.Bootstrapper;

namespace OpenOrderSystem.Core.Ui;

/// <summary>
/// The controllers under <c>Ui/</c>, with the folder each one lives in (its URL prefix) and the boot
/// modes it is active in.
/// </summary>
/// <param name="Type">The controller class.</param>
/// <param name="Name">The MVC controller name (class name without <c>Controller</c>).</param>
/// <param name="Folder">The folder under <c>Ui/</c>, e.g. <c>Staff/Dashboard</c>.</param>
/// <param name="Modes">The boot mode keys from <see cref="OosUiActiveModeAttribute"/>.</param>
/// <param name="IsHome">Whether the controller has <see cref="OosUiHomeAttribute"/>.</param>
public sealed record UiController(Type Type, string Name, string Folder, IReadOnlyList<string> Modes, bool IsHome)
{
    public bool IsActiveIn(string modeKey) => Modes.Contains(modeKey, StringComparer.OrdinalIgnoreCase);
}

public static class UiControllers
{
    private static readonly Lazy<IReadOnlyList<UiController>> _all = new(Scan);

    /// <summary>
    /// Every UI controller. The first access scans the assembly and throws if any UI controller is
    /// set up wrong, listing every problem at once.
    /// </summary>
    public static IReadOnlyList<UiController> All => _all.Value;

    /// <summary>The UI controllers active in the given boot mode.</summary>
    public static IEnumerable<UiController> ActiveIn(string modeKey) => All.Where(c => c.IsActiveIn(modeKey));

    /// <summary>The home controller active in the given boot mode, if it has one.</summary>
    public static UiController? HomeIn(string modeKey) => ActiveIn(modeKey).SingleOrDefault(c => c.IsHome);

    private static IReadOnlyList<UiController> Scan()
    {
        var errors = new List<string>();
        var found = new List<UiController>();

        var knownModes = OpenOrderSystemApplication.FindBootModeTypes()
            .Select(OpenOrderSystemApplication.GetBootModeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uiNamespace = typeof(UiControllers).Namespace!;

        foreach (var type in typeof(UiControllers).Assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<OosUiActiveModeAttribute>(inherit: false);
            var isHome = type.IsDefined(typeof(OosUiHomeAttribute), inherit: false);

            if (isHome && attribute is null)
                errors.Add($"{type.FullName} has [OosUiHome] but no [OosUiActiveMode], so it is never active.");
            var isController = type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type);
            var inUi = type.Namespace is { } ns &&
                       (ns == uiNamespace || ns.StartsWith(uiNamespace + ".", StringComparison.Ordinal));

            if (attribute is null)
            {
                if (isController && inUi)
                    errors.Add($"{type.FullName} is a UI controller but has no [OosUiActiveMode]. Add one listing the boot modes it is active in.");
                continue;
            }

            if (!isController)
            {
                errors.Add($"{type.FullName} has [OosUiActiveMode] but is not a non-abstract controller.");
                continue;
            }

            if (!UiFolder.TryGet(type, out var folder))
            {
                errors.Add($"{type.FullName} has [OosUiActiveMode] but is not in a folder under Ui/ " +
                           $"(its namespace must start with {uiNamespace}.), so it has no URL.");
                continue;
            }

            if (type.IsGenericTypeDefinition)
                errors.Add($"{type.FullName} is an open generic controller, which cannot be routed.");

            if (attribute.Modes.Count == 0)
                errors.Add($"{type.FullName} has [OosUiActiveMode] with no boot modes.");

            foreach (var mode in attribute.Modes.Where(m => !knownModes.Contains(m ?? "")))
                errors.Add($"{type.FullName} lists unknown boot mode \"{mode}\". Known modes: {string.Join(", ", knownModes.Order())}.");

            var name = type.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? type.Name[..^"Controller".Length]
                : type.Name;

            found.Add(new UiController(type, name, folder, attribute.Modes, isHome));
        }

        // Only one controller can own "/" in a mode.
        foreach (var mode in knownModes.Order())
        {
            var homes = found.Where(c => c.IsHome && c.IsActiveIn(mode)).ToList();
            if (homes.Count > 1)
                errors.Add($"Boot mode \"{mode}\" has more than one active [OosUiHome] controller ({string.Join(", ", homes.Select(c => c.Type.FullName))}).");
        }

        // Two controllers in one folder would share a URL prefix.
        foreach (var group in found.GroupBy(c => c.Folder, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Ui/{group.Key} holds more than one controller ({string.Join(", ", group.Select(c => c.Type.FullName))}); each folder is one URL and takes one controller.");

        if (errors.Count > 0)
        {
            var message = new StringBuilder().AppendLine("UI controller setup failed:");
            foreach (var error in errors) message.Append("  - ").AppendLine(error);
            throw new InvalidOperationException(message.ToString().TrimEnd());
        }

        return found;
    }
}
