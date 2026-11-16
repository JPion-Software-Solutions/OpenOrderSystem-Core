namespace OpenOrderSystem.Core.Services.Devices.Types;

/// <summary>
/// Whether a device type talks to OOS itself or only through a parent device.
/// </summary>
/// <remarks>
/// Every device tree has exactly one principal device, at its root. Every other device in the tree is
/// a dependent, at any depth.
/// </remarks>
public enum DeviceRole
{
    /// <summary>
    /// Has its own identity and credentials and talks to OOS directly, such as a print bridge or a
    /// staff order terminal. A principal device is always the root of its device tree and never has a
    /// parent. It claims the jobs for itself and for every dependent device in its tree.
    /// </summary>
    Principal,

    /// <summary>
    /// Never talks to OOS. Always has a parent, and sits somewhere below the principal device at the
    /// root of its tree, such as a printer under a print bridge. A dependent device's jobs are claimed by
    /// the principal device at the root of its tree.
    /// </summary>
    Dependent,
}
