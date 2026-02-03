using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class BootModeUiAttribute : Attribute
{
    /// <summary>
    /// If false, this mode should not be shown in any user-facing boot mode picker.
    /// </summary>
    public bool ShowInUi { get; init; } = true;

    /// <summary>
    /// Optional description to help users decide what mode to select.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional override for the display label in UI (keeps key convention-based).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Optional: mark as dangerous/advanced so UI can style it differently.
    /// </summary>
    public bool Advanced { get; init; } = false;
}

