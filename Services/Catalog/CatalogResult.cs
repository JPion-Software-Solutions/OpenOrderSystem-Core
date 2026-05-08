namespace OpenOrderSystem.Core.Services.Catalog;

/// <summary>
/// Base result type for all catalog store operations. Carries a status code and optional
/// diagnostic message; use <see cref="IsSuccess"/> for simple success checks or branch on
/// <see cref="Status"/> for fine-grained error handling.
/// </summary>
public class CatalogResult
{
    /// <summary>
    /// The outcome of the catalog operation.
    /// </summary>
    public CatalogResultStatus Status { get; init; } = CatalogResultStatus.Success;

    /// <summary>
    /// Optional human-readable message providing additional context about the result.
    /// Typically populated on failure but may carry informational text on success.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// <see langword="true"/> if the operation completed successfully; otherwise <see langword="false"/>.
    /// </summary>
    public bool IsSuccess => (int)Status < 101;
}

/// <summary>
/// Describes the outcome of a catalog store operation.
/// </summary>
/// <remarks>
/// Success codes are in the range 1–100, leaving room for additional success modes.
/// Error codes begin at 101.
/// </remarks>
public enum CatalogResultStatus
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Success = 1,

    // -------------------------
    // Error codes (101+)
    // -------------------------

    /// <summary>
    /// The requested catalog entity could not be found.
    /// </summary>
    NotFound = 101,

    /// <summary>
    /// The supplied data failed validation.
    /// </summary>
    ValidationError = 102,

    /// <summary>
    /// The operation would create a duplicate or violate a uniqueness constraint.
    /// </summary>
    Conflict = 103,

    /// <summary>
    /// An unexpected storage or I/O failure occurred.
    /// </summary>
    StorageError = 104,
}
