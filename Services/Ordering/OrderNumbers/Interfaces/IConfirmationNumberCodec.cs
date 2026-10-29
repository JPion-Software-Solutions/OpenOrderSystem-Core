namespace OpenOrderSystem.Core.Services.Ordering.OrderNumbers.Interfaces;

/// <summary>
/// Converts between plain order numbers and the scrambled confirmation numbers given to customers.
/// </summary>
/// <remarks>
/// The scramble is a keyed permutation of the 8-digit range, so it is reversible and every order number maps
/// to exactly one confirmation number. Its purpose is to stop a customer guessing a neighbouring order's
/// confirmation number in the order tracker; it is not a security boundary.
/// </remarks>
public interface IConfirmationNumberCodec
{
    /// <summary>The confirmation number for an order number.</summary>
    Task<int> ToConfirmationNumberAsync(int orderNumber);

    /// <summary>The order number a confirmation number refers to.</summary>
    Task<int> ToOrderNumberAsync(int confirmationNumber);
}
