using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

public class ProductSnapshot
{
    /// <summary>
    /// Unique identifier for this snapshot row.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The live product this snapshot was taken from.
    /// </summary>
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>
    /// Links to the prior snapshot in this product's snapshot chain (if any).
    /// </summary>
    public Guid? PreviousSnapshotId { get; set; }
    public ProductSnapshot? PreviousSnapshot { get; set; }

    /// <summary>
    /// Snapshot schema version to allow evolving the JSON shape over time.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// JSON blob containing the full frozen product graph:
    /// product + variants + options + cover media + album membership, etc.
    /// </summary>
    public string SnapshotJson { get; set; } = string.Empty;

    /// <summary>
    /// Optional hash of SnapshotJson (or of the canonical snapshot DTO) for dedupe/integrity.
    /// </summary>
    public string? SnapshotHash { get; set; }

    /// <summary>
    /// When the snapshot row was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}