using System;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Core;

/// <summary>
/// Represents a single configuration entry in the OOS system configuration store.
/// </summary>
/// <remarks>
/// <para>
/// The system configuration store is a flat key-value table used for site-wide
/// operational configuration. Keys should be namespaced by convention using dot
/// separation to avoid collisions across subsystems
/// (e.g., <c>routing.OrderReceived.printer-kitchen-1</c>).
/// </para>
/// <para>
/// Locked entries (<see cref="IsLocked"/>) are protected from modification through
/// normal operator-facing configuration interfaces and should only be written by
/// the system itself.
/// </para>
/// </remarks>
public class SystemConfig
{
    /// <summary>
    /// The unique configuration key. By convention, keys are dot-separated and
    /// namespaced by subsystem (e.g., <c>routing.OrderReceived.printer-kitchen-1</c>).
    /// </summary>
    [Key]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The configuration value stored as a string. Complex values should be
    /// serialized as JSON.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The UTC timestamp of the last write to this configuration entry.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether this entry is locked against modification through
    /// operator-facing configuration interfaces. Locked entries are written
    /// and managed exclusively by the system.
    /// </summary>
    public bool IsLocked { get; set; }
}