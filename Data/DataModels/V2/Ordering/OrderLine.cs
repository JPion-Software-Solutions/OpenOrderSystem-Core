using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Represents a single line item on an <see cref="OrderHeader"/>, capturing a frozen snapshot
/// of the product, variant, and option selections made by the customer at the time of ordering.
/// </summary>
/// <remarks>
/// <para>
/// Core display fields (<see cref="ProductName"/>, <see cref="VariantName"/>, <see cref="UnitPrice"/>)
/// are denormalized directly onto the line for efficient access without requiring snapshot deserialization.
/// </para>
/// <para>
/// A non-nullable reference to <see cref="ProductSnapshot"/> is required at order creation time,
/// providing a full frozen product graph for auditing and historical reconstruction.
/// </para>
/// <para>
/// <b>Warning:</b> Writing to <see cref="SelectedOptionsJson"/> directly is not recommended.
/// Use <see cref="SetOptions"/> and access options via <see cref="SelectedOptions"/> to keep
/// cached state coherent and avoid persisting invalid JSON.
/// </para>
/// </remarks>
public class OrderLine
{
    private IReadOnlyList<OrderLineOption>? _optionsCache;
    private string? _optionsJsonCache;

    /// <summary>
    /// Unique identifier for this order line.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the parent <see cref="OrderHeader"/>.
    /// </summary>
    public Guid OrderHeaderId { get; set; }

    /// <summary>
    /// Navigation property for the parent <see cref="OrderHeader"/>.
    /// </summary>
    public OrderHeader? OrderHeader { get; set; }

    /// <summary>
    /// Foreign key to the <see cref="ProductSnapshot"/> capturing the full frozen product graph
    /// at the time this line was created. Required at order creation time.
    /// </summary>
    public Guid ProductSnapshotId { get; set; }

    /// <summary>
    /// Navigation property for the frozen product snapshot associated with this line.
    /// </summary>
    public ProductSnapshot? ProductSnapshot { get; set; }

    /// <summary>
    /// Soft reference to the originating <see cref="Product"/> row in the live catalog.
    /// May be <see langword="null"/> if the product has since been deleted.
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Navigation property for the originating live catalog product.
    /// </summary>
    public Product? Product { get; set; }

    /// <summary>
    /// Display name of the product, frozen at order time.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the selected variant, frozen at order time.
    /// </summary>
    public string VariantName { get; set; } = string.Empty;

    /// <summary>
    /// Base unit price of the selected variant, frozen at order time.
    /// </summary>
    /// <remarks>
    /// Option deltas are not included in this value. Use the service layer to compute
    /// the effective line total from <see cref="UnitPrice"/> and <see cref="SelectedOptions"/>.
    /// </remarks>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Number of units ordered.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Optional comments or special instructions provided by the customer for this line item.
    /// </summary>
    public string? LineComments { get; set; }

    /// <summary>
    /// JSON storage for <see cref="SelectedOptions"/>.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> Writing to this property directly is not recommended.
    /// Use <see cref="SetOptions"/> to keep cached state coherent and avoid persisting invalid JSON.
    /// </remarks>
    public string? SelectedOptionsJson { get; set; }

    /// <summary>
    /// The options selected, added, or removed by the customer for this line item, frozen at order time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is not mapped by EF Core. It is backed by <see cref="SelectedOptionsJson"/>
    /// and uses cached deserialization.
    /// </para>
    /// <para>
    /// Options with <see cref="OptionSelectionState.Included"/> are implied by the base product
    /// configuration and are typically not surfaced in customer-facing displays. Only
    /// <see cref="OptionSelectionState.Added"/> and <see cref="OptionSelectionState.Removed"/>
    /// entries represent customer deviations from the default.
    /// </para>
    /// </remarks>
    [NotMapped]
    public IReadOnlyList<OrderLineOption> SelectedOptions => EnsureOptionsLoaded();

    /// <summary>
    /// Replaces the current set of selected options with the provided list and persists to <see cref="SelectedOptionsJson"/>.
    /// </summary>
    /// <param name="options">The options to store on this line.</param>
    public void SetOptions(IEnumerable<OrderLineOption> options)
    {
        var list = options.ToList();
        _optionsCache = list;
        SelectedOptionsJson = list.Count == 0
            ? null
            : JsonSerializer.Serialize(list);
        _optionsJsonCache = SelectedOptionsJson;
    }

    private IReadOnlyList<OrderLineOption> EnsureOptionsLoaded()
    {
        if (_optionsCache is not null && _optionsJsonCache == SelectedOptionsJson)
            return _optionsCache;

        if (string.IsNullOrWhiteSpace(SelectedOptionsJson))
        {
            _optionsCache = [];
            _optionsJsonCache = SelectedOptionsJson;
            return _optionsCache;
        }

        try
        {
            _optionsCache = JsonSerializer.Deserialize<List<OrderLineOption>>(SelectedOptionsJson)
                            ?? [];
        }
        catch
        {
            _optionsCache = [];
        }

        _optionsJsonCache = SelectedOptionsJson;
        return _optionsCache;
    }
}

/// <summary>
/// Represents the state of a single option on an <see cref="OrderLine"/> at the time the order was placed.
/// </summary>
/// <param name="OptionName">
/// Display name of the option frozen at order time (e.g., "Pepperoni", "Extra Cheese", "No Onion").
/// </param>
/// <param name="Delta">
/// The raw price delta from the originating <see cref="Option"/> row, frozen at order time.
/// Represents the catalog-defined cost of this option independent of any pricing strategy.
/// </param>
/// <param name="EffectiveDelta">
/// The actual price impact applied to the line at order time after pricing strategy resolution.
/// For example, an included option with a non-zero <paramref name="Delta"/> will typically have
/// an <paramref name="EffectiveDelta"/> of zero under the simple pricing model.
/// </param>
/// <param name="Quantity">
/// The quantity of this option on the line. A value of <c>0</c> indicates the option was explicitly
/// removed by the customer. A value of <c>1</c> is standard, values greater than <c>1</c> indicate
/// extras, and fractional values such as <c>0.5</c> indicate a reduced quantity (e.g., light sauce).
/// Defaults to <c>1.0</c>.
/// </param>
/// <param name="SelectionState">
/// Indicates whether this option was included by default, explicitly added, or explicitly removed
/// by the customer. <see cref="OptionSelectionState.Included"/> options are implied by the base
/// item and are typically not surfaced in customer-facing displays unless their quantity deviates
/// from the default. Defaults to <see cref="OptionSelectionState.Included"/>.
/// </param>
public record OrderLineOption(
    string OptionName,
    decimal Delta,
    decimal EffectiveDelta,
    decimal Quantity = 1.0m,
    OptionSelectionState SelectionState = OptionSelectionState.Included
);

/// <summary>
/// Describes how an option relates to an <see cref="OrderLine"/> relative to the product's defaults.
/// </summary>
public enum OptionSelectionState
{
    /// <summary>
    /// The option is included by default in the base product configuration.
    /// Implied at the UI level and typically not displayed to the customer unless removed.
    /// </summary>
    Included = 0,

    /// <summary>
    /// The option was explicitly added by the customer beyond the base product configuration.
    /// </summary>
    Added = 1,

    /// <summary>
    /// The option was explicitly removed by the customer from the base product configuration.
    /// </summary>
    Removed = 2
}