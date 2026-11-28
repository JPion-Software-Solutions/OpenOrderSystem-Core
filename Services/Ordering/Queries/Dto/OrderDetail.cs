using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

namespace OpenOrderSystem.Core.Services.Ordering.Queries.Dto;

/// <summary>
/// A read-only view of one order with everything on it: customer, lines, stage history and metadata.
/// </summary>
public sealed record OrderDetail : OrderSummary
{
    /// <summary>The scrambled confirmation number the customer was given.</summary>
    public required int ConfirmationNumber { get; init; }

    /// <summary>The customer, or <see langword="null"/> when the order has no customer record.</summary>
    public OrderCustomer? Customer { get; init; }

    /// <summary>
    /// The order's lines, by product name and then variant name. Lines store no position, so the order they
    /// were added to the cart is not kept.
    /// </summary>
    public required IReadOnlyList<OrderLineView> Lines { get; init; }

    /// <summary>Every stage the order has been in, oldest first. The last entry is open while the order is in it.</summary>
    public required IReadOnlyList<OrderStageVisit> StageHistory { get; init; }

    /// <summary>The order's metadata, as stored.</summary>
    public required IReadOnlyDictionary<string, JsonElement> Metadata { get; init; }
}

/// <summary>
/// The customer on an order.
/// </summary>
public sealed record OrderCustomer(Guid Id, string Name, string Phone, string Email);

/// <summary>
/// One line of an order.
/// </summary>
/// <param name="Id">The line's ID.</param>
/// <param name="ProductId">The product, or <see langword="null"/> if it has since been deleted.</param>
/// <param name="ProductName">The product name when the order was placed.</param>
/// <param name="VariantName">The variant name when the order was placed.</param>
/// <param name="UnitPrice">The price of one item, options included.</param>
/// <param name="Quantity">How many.</param>
/// <param name="LineTotal"><paramref name="UnitPrice"/> times <paramref name="Quantity"/>.</param>
/// <param name="LineComments">The comments on this line.</param>
/// <param name="Options">The options chosen, as recorded when the order was placed.</param>
public sealed record OrderLineView(
    Guid Id,
    Guid? ProductId,
    string ProductName,
    string VariantName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string? LineComments,
    IReadOnlyList<OrderLineOption> Options);

/// <summary>
/// One stage an order has been in.
/// </summary>
/// <param name="StageId">The stage.</param>
/// <param name="StageName">Its name, or an empty string if the stage has since been deleted.</param>
/// <param name="EnteredAt">When the order entered it.</param>
/// <param name="ExitedAt">When the order left it, or <see langword="null"/> if it is still there.</param>
public sealed record OrderStageVisit(Guid StageId, string StageName, DateTimeOffset EnteredAt, DateTimeOffset? ExitedAt);
