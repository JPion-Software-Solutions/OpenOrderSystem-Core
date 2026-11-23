using System.Diagnostics.CodeAnalysis;

namespace OpenOrderSystem.Core.Ui;

/// <summary>
/// Maps a UI controller to its folder under <c>Ui/</c>. The folder comes from the controller's namespace,
/// so the namespace must match the folder: <c>OpenOrderSystem.Core.Ui.Staff.Dashboard</c> lives in
/// <c>Ui/Staff/Dashboard/</c>. Views sit in the same folder, and the folder path is the URL prefix.
/// </summary>
public static class UiFolder
{
    private static readonly string _rootNamespace = typeof(UiFolder).Namespace!;

    /// <summary>
    /// Gets the folder for <paramref name="type"/> relative to <c>Ui/</c> (e.g. <c>Staff/Dashboard</c>).
    /// Returns false for types outside the Ui namespace.
    /// </summary>
    public static bool TryGet(Type type, [NotNullWhen(true)] out string? folder)
    {
        folder = null;
        var ns = type.Namespace;

        if (ns is null || !ns.StartsWith(_rootNamespace + ".", StringComparison.Ordinal))
            return false;

        folder = ns[(_rootNamespace.Length + 1)..].Replace('.', '/');
        return true;
    }
}
