using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

namespace OpenOrderSystem.Core.Services.Ordering.CartServices.Dto;

/// <summary>
/// Describes a single option selection to apply to a cart line, used as input to
/// <see cref="Interfaces.ICartService.SetLineOptionsAsync"/>. The cart service resolves
/// display names and pricing from the catalog — callers supply only the option identity and intent.
/// </summary>
public class CartLineOptionRequest
{
    /// <summary>The catalog option to apply.</summary>
    public Guid OptionId { get; set; }

    /// <summary>
    /// Quantity of the option. <c>0</c> = explicitly removed, <c>0.5</c> = light,
    /// <c>1</c> = standard, greater than <c>1</c> = extra. Defaults to <c>1</c>.
    /// </summary>
    public decimal Quantity { get; set; } = 1.0m;

    /// <summary>Whether this option is being added or removed relative to the product default.</summary>
    public OptionSelectionState SelectionState { get; set; } = OptionSelectionState.Added;
}

/// <summary>
/// The full draft order state serialized into <see cref="Data.DataModels.V2.Ordering.Cart.DraftOrder"/>.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="CartDraft"/> is the working representation of an order-in-progress. It carries
/// enough information to render the cart UI without querying the catalog, and enough to project
/// into an <see cref="OrderHeader"/> with associated <see cref="OrderLine"/> records at submission
/// time. Prices and display names are frozen when items are added — catalog changes during an
/// active session do not affect the cart.
/// </para>
/// <para>
/// <see cref="Customer"/> is nullable because items are typically added before the customer
/// provides their details at checkout. Submission requires a non-null <see cref="Customer"/>.
/// </para>
/// </remarks>
public class CartDraft
{
    /// <summary>
    /// Customer details to be written into a <see cref="CustomerRecord"/> at submission time.
    /// Null until the customer completes the checkout step.
    /// </summary>
    public CartCustomerInfo? Customer { get; set; }

    /// <summary>
    /// Optional order-level comments or special instructions from the customer.
    /// Maps to <see cref="OrderHeader.OrderComments"/> at submission time.
    /// </summary>
    public string? OrderComments { get; set; }

    /// <summary>
    /// The customer's requested fulfillment time, or <see langword="null"/> for ASAP.
    /// The order service resolves this to an actual assigned slot at submission time.
    /// </summary>
    public DateTimeOffset? RequestedTimeSlot { get; set; }

    /// <summary>
    /// The line items currently in the cart.
    /// </summary>
    public List<CartDraftLine> Lines { get; set; } = [];
}

/// <summary>
/// Customer contact and notification details captured during checkout.
/// Becomes a <see cref="CustomerRecord"/> at submission time.
/// </summary>
public class CartCustomerInfo
{
    /// <summary>Full name of the customer.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Phone number of the customer.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Email address of the customer.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The customer's preferred notification channels.</summary>
    public CustomerNotificationPreferences NotificationPreferences { get; set; } =
        CustomerNotificationPreferences.None;
}

/// <summary>
/// A single line item in a <see cref="CartDraft"/>.
/// </summary>
/// <remarks>
/// Display fields (<see cref="ProductName"/>, <see cref="VariantName"/>, <see cref="BasePrice"/>)
/// are frozen at the time the item is added to the cart. <see cref="ProductId"/> and
/// <see cref="VariantId"/> are used at submission time to create a <see cref="ProductSnapshot"/>
/// and populate the corresponding <see cref="OrderLine"/>.
/// </remarks>
public class CartDraftLine
{
    /// <summary>
    /// Stable identifier for this line within the draft. Used by cart mutation APIs to
    /// reference a specific line for update or removal without relying on list position.
    /// </summary>
    public Guid LineId { get; set; } = Guid.NewGuid();

    /// <summary>Soft reference to the live catalog product. Used for snapshot creation at submission.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The selected variant. Used for snapshot creation and pricing at submission.</summary>
    public Guid VariantId { get; set; }

    /// <summary>Product display name, frozen at the time the item was added.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Variant display name, frozen at the time the item was added.</summary>
    public string VariantName { get; set; } = string.Empty;

    /// <summary>
    /// Base unit price of the selected variant, frozen at the time the item was added.
    /// Does not include option deltas — compute the effective line total from this value
    /// plus the sum of <see cref="CartDraftOption.EffectiveDelta"/> across <see cref="SelectedOptions"/>.
    /// </summary>
    public decimal BasePrice { get; set; }

    /// <summary>Number of units.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Optional customer comments or special instructions for this line.</summary>
    public string? LineComments { get; set; }

    /// <summary>The options selected, added, or removed by the customer for this line.</summary>
    public List<CartDraftOption> SelectedOptions { get; set; } = [];
}

/// <summary>
/// A single option selection on a <see cref="CartDraftLine"/>.
/// </summary>
/// <remarks>
/// All pricing and display fields are frozen at the time the option is applied to the cart line.
/// <see cref="OptionId"/> is retained for snapshot and validation use at submission time.
/// </remarks>
public class CartDraftOption
{
    /// <summary>Soft reference to the live catalog option. Used for snapshot creation at submission.</summary>
    public Guid OptionId { get; set; }

    /// <summary>Option display name, frozen at the time the option was applied.</summary>
    public string OptionName { get; set; } = string.Empty;

    /// <summary>Raw catalog price delta for this option, frozen at add time.</summary>
    public decimal Delta { get; set; }

    /// <summary>
    /// Resolved price impact after applying pricing strategy (e.g., zero for included options),
    /// frozen at add time. This is the value that contributes to the line total.
    /// </summary>
    public decimal EffectiveDelta { get; set; }

    /// <summary>
    /// Quantity of this option on the line. <c>0</c> = explicitly removed, <c>0.5</c> = light,
    /// <c>1</c> = standard, greater than <c>1</c> = extra. Defaults to <c>1</c>.
    /// </summary>
    public decimal Quantity { get; set; } = 1.0m;

    /// <summary>
    /// Whether this option was part of the product default, explicitly added, or explicitly removed.
    /// Maps directly to <see cref="OptionSelectionState"/> on <see cref="OrderLineOption"/>.
    /// </summary>
    public OptionSelectionState SelectionState { get; set; } = OptionSelectionState.Included;
}