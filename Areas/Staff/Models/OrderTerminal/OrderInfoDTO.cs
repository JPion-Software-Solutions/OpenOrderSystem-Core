namespace OpenOrderSystem.Core.Areas.Staff.Models.OrderTerminal;

/// <summary>
/// A finished snapshot of a single order for display on the Staff Order Terminal.
/// All pricing (<see cref="SubTotal"/>, <see cref="PriceAdjustments"/>, <see cref="Total"/>)
/// is expected to be calculated upstream before this DTO is constructed; it holds data
/// only and performs no calculations of its own.
/// </summary>
public class OrderInfoDTO
{
    /// <summary>The order's database identifier.</summary>
    public int OrderId { get; set; }

    /// <summary>The customer-facing confirmation number for this order.</summary>
    public int OrderConfirmationNumber { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    
    public DateTimeOffset OrderPickupTime { get; set; } = DateTimeOffset.Now;

    /// <summary>The order's line items.</summary>
    public List<OrderLineRecord> OrderLines { get; set; } = new List<OrderLineRecord>();

    /// <summary>Sum of all order line prices before adjustments.</summary>
    public double SubTotal { get; set; } = 0;

    /// <summary>Individual price adjustments applied to the order (e.g. discounts, surcharges, fees).</summary>
    public List<PriceAdjustmentRecord> PriceAdjustments { get; set; } = new List<PriceAdjustmentRecord>();

    /// <summary>The final order total, i.e. <see cref="SubTotal"/> plus the sum of <see cref="PriceAdjustments"/>.</summary>
    public double Total { get; set; } = 0;
}

/// <summary>
/// A single item on an order, including which customizable options were selected,
/// added, or removed from its default configuration.
/// </summary>
public record OrderLineRecord
{
    /// <summary>The name of the item ordered (e.g. "Large Pepperoni Pizza").</summary>
    public string ItemName { get; set; } = string.Empty;
    
    public string VariantName { get; set; } = string.Empty;

    /// <summary>All options selected for this line item.</summary>
    public List<string> FullOptionsList { get; set; } = new List<string>();

    /// <summary>Selected options grouped by category (e.g. "Toppings", "Size").</summary>
    public Dictionary<string, List<string>> OptionsListByCategory { get; set; } = new Dictionary<string, List<string>>();

    /// <summary>Options removed from this line's default configuration, if any.</summary>
    public List<string>? RemovedOptionsList { get; set; } = null;

    /// <summary>Options added beyond this line's default configuration, if any.</summary>
    public List<string>? AddedOptionsList { get; set; } = null;

    /// <summary>The price of this line item, including its selected options.</summary>
    public double LinePrice { get; set; } = 0;
}

/// <summary>A single named price adjustment applied to an order.</summary>
/// <param name="AdjustmentName">A human-readable label for the adjustment (e.g. "Manager Discount", "Delivery Fee").</param>
/// <param name="Price">The amount of the adjustment; negative for discounts, positive for surcharges/fees.</param>
public record PriceAdjustmentRecord(string AdjustmentName, double Price);