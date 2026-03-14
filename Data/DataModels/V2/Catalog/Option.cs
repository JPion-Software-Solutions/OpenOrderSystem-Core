using System;
using System.ComponentModel.DataAnnotations;
using OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

public class Option : IGroupMember<OptionGroup, Option>
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Customer-facing display label. Keep short for UI (e.g., &lt;= 40 chars).
    /// Examples: "Pepperoni", "Extra Cheese", "No Onion".
    /// </summary>
    [MaxLength(40)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Price adjustment applied when selected. Default is 0.
    /// </summary>
    public decimal PriceDelta { get; set; }

    /// <summary>
    /// Flags controlling availability/visibility and other option behaviors.
    /// </summary>
    public OptionFlags Flags { get; set; } = OptionFlags.None;
    public Guid? GroupId { get; set;}
    public OptionGroup? Group { get; set;}
}

[Flags]
public enum OptionFlags
{
    None = 0,

    /// <summary>
    /// Option is temporarily unavailable and cannot be selected.
    /// </summary>
    OutOfStock = 1 << 0,

    /// <summary>
    /// Option is hidden from customers but may be visible to staff/admin tools.
    /// </summary>
    Hidden = 1 << 1,

    /// <summary>
    /// Option is disabled/retired and cannot be selected.
    /// </summary>
    Disabled = 1 << 2
}
