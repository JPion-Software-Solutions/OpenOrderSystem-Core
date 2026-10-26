using OpenOrderSystem.Core.Areas.Staff.Models.OrderTerminal;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.OrderTerminal;

/// <summary>
/// The board view of the Staff Order Terminal: a set of stages (rows), each holding the
/// orders currently in that stage. An authenticated staff user can view this in one of two
/// modes: as a <see cref="IsRegisteredTerminal">registered terminal</see> with a unique
/// identity that drives real system state (e.g. terminal online status, interacting with
/// orders), or as an unregistered/view-only terminal that can observe the board but not
/// act on it. Both modes require authentication; the registered/unregistered distinction
/// is about terminal identity, not login state.
/// </summary>
public class OrderTerminalVM
{
    /// <summary>The registered terminal's unique identity, or null if this is an unregistered/view-only terminal.</summary>
    public Guid? OrderTerminalId { get; set; } = null;

    /// <summary>The registered terminal's display name, or null if this is an unregistered/view-only terminal.</summary>
    public string? OrderTerminalName { get; set; } = null;

    /// <summary>Whether this terminal has a registered identity and can drive system state (as opposed to view-only).</summary>
    public bool IsRegisteredTerminal => OrderTerminalId is not null;

    /// <summary>The board's stages (columns), in display order.</summary>
    public List<StageInfo> Stages { get; set; } = new List<StageInfo>();

    /// <summary>An order sitting in a particular stage.</summary>
    /// <param name="OrderInfo">The order's data.</param>
    /// <param name="EnteredStageAt">When the order entered this stage; used to derive its age/auto-exit deadline.</param>
    public record StageOrder(OrderInfoDTO OrderInfo, DateTimeOffset EnteredStageAt);

    /// <summary>A single stage (column) on the order board and the orders currently in it.</summary>
    /// <param name="StageName">The stage's display name.</param>
    /// <param name="Orders">The orders currently in this stage.</param>
    /// <param name="Metadata">
    /// Raw JSON blob for stage display/config data (e.g. color, icon). Deliberately untyped
    /// as a stopgap until the V2 database model ships and takes ownership of this data.
    /// </param>
    /// <param name="AutoExitMinutes">
    /// If set, the number of minutes an order may sit in this stage before it should auto-exit.
    /// Combine with a <see cref="StageOrder"/>'s <c>EnteredStageAt</c> to derive its deadline;
    /// not stored per-order so a rule change here doesn't leave stale deadlines behind.
    /// </param>
    public record StageInfo(string StageName, List<StageOrder> Orders, string Metadata = "{}", int? AutoExitMinutes = null);
}