namespace OpenOrderSystem.Core.Ui;

/// <summary>
/// Lists the boot modes a UI controller is active in, by boot mode key (e.g. <c>run_oos</c>,
/// <c>recovery</c>). Every controller under <c>Ui/</c> must carry it. In other modes the controller is
/// removed from MVC and gets no route.
/// </summary>
/// <example><c>[OosUiActiveMode("run_oos", "recovery")]</c></example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OosUiActiveModeAttribute(params string[] modes) : Attribute
{
    /// <summary>The boot mode keys this controller is active in.</summary>
    public IReadOnlyList<string> Modes { get; } = modes;
}
