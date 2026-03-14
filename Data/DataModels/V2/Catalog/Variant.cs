using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

/// <summary>
/// Represents a purchasable variation of a <c>Product</c> (e.g., size, package, or other sellable configuration).
/// </summary>
/// <remarks>
/// <para>
/// Variants carry commerce-facing identifiers and pricing (e.g., <see cref="Sku"/>, <see cref="Barcode"/>, <see cref="Price"/>).
/// </para>
/// <para>
/// This model uses JSON-backed extension fields (<see cref="BarcodeJson"/> and <see cref="MetadataJson"/>) with cached
/// deserialization to avoid repeatedly parsing JSON on every access.
/// </para>
/// <para>
/// <b>Important:</b> Avoid writing to <see cref="BarcodeJson"/> and <see cref="MetadataJson"/> directly in application code.
/// Doing so bypasses cache coherence and validation conventions and can result in stale in-memory values and/or invalid JSON
/// being persisted. Prefer using <see cref="Barcode"/> and the metadata helpers (<see cref="SetMetadata(string, JsonElement)"/>,
/// <see cref="SetMetadata{T}(string, T)"/>, <see cref="RemoveMetadata(string)"/>).
/// </para>
/// </remarks>
public class Variant : IGroupMember<VariantGroup, Variant>
{
    private BarcodeRecord? _barcodeCache;
    private string? _barcodeJsonCache;

    private Dictionary<string, JsonElement>? _metadataCache;
    private string? _metadataJsonCache;

    /// <summary>
    /// Unique identifier for this variant.
    /// </summary>
    /// <remarks>
    /// Intended to be immutable after creation. External integrations should prefer stable identifiers (e.g., <see cref="Sku"/> and/or barcodes),
    /// but <see cref="Id"/> provides a durable internal identity.
/// </remarks>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-friendly name for this variant (e.g., "Small", "Large", "16oz").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base selling price of this variant.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="decimal"/> to avoid floating point rounding errors.
    /// </remarks>
    public decimal Price { get; set; }

    /// <summary>
    /// Optional "compare at" or strike-through price (e.g., original price before a sale).
    /// </summary>
    public decimal? StrikePrice { get; set; }

    /// <summary>
    /// Optional internal cost basis for reporting/margin calculations.
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// Optional stock keeping unit (SKU) for internal catalog/inventory use.
    /// </summary>
    /// <remarks>
    /// SKUs are typically organization-defined identifiers and are commonly unique within a tenant/store scope.
    /// </remarks>
    public string? Sku { get; set; }

    /// <summary>
    /// JSON storage for <see cref="Barcode"/>.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> Writing to this property directly is not recommended.
    /// Prefer using <see cref="Barcode"/> to keep cached state coherent and to enforce a consistent serialization format.
    /// </remarks>
    public string? BarcodeJson { get; set; }

    /// <summary>
    /// JSON storage for <see cref="Metadata"/>.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> Writing to this property directly is not recommended.
    /// Prefer using metadata helper methods (<see cref="SetMetadata(string, JsonElement)"/>, <see cref="SetMetadata{T}(string, T)"/>,
    /// <see cref="RemoveMetadata(string)"/>) to keep cached state coherent and to avoid persisting invalid JSON.
    /// </remarks>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Structured barcode information for this variant.
    /// </summary>
    /// <remarks>
    /// This property is not mapped by EF Core. It is backed by <see cref="BarcodeJson"/> and uses cached deserialization.
    /// If the stored JSON is invalid, this returns <see langword="null"/> and the service layer is expected to decide how to handle it.
    /// </remarks>
    [NotMapped]
    public BarcodeRecord? Barcode
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BarcodeJson)) return null;
            if (_barcodeJsonCache == BarcodeJson) return _barcodeCache;

            try
            {
                _barcodeCache = JsonSerializer.Deserialize<BarcodeRecord>(BarcodeJson);
            }
            catch
            {
                _barcodeCache = null;
            }

            _barcodeJsonCache = BarcodeJson;
            return _barcodeCache;
        }
        set
        {
            _barcodeCache = value;
            _barcodeJsonCache = null;
            BarcodeJson = value is null ? null : JsonSerializer.Serialize(value);
        }
    }

    /// <summary>
    /// Arbitrary extension metadata for this variant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is not mapped by EF Core. It is backed by <see cref="MetadataJson"/> and uses cached deserialization.
    /// </para>
    /// <para>
    /// Metadata is intended for non-core, non-indexed extension fields (e.g., plugin/vendor specific values, UI hints, payment 
    /// gateway linked products). If a value must be queried, indexed, or validated as part of core business rules, it should 
    /// be promoted to a proper column.
    /// </para>
    /// <para>
    /// The returned dictionary is read-only to prevent accidental in-memory mutation without serialization. Use
    /// <see cref="SetMetadata(string, JsonElement)"/>, <see cref="SetMetadata{T}(string, T)"/>, and <see cref="RemoveMetadata(string)"/>
    /// to modify metadata.
    /// </para>
    /// </remarks>
    [NotMapped]
    public IReadOnlyDictionary<string, JsonElement> Metadata => EnsureMetadataLoaded();

    public Guid? GroupId { get; set;}
    public virtual VariantGroup? Group { get; set;}

    public Guid ProductId { get; set; }
    public virtual Product? Product { get; set; }

    /// <summary>
    /// Sets a metadata key to the specified JSON value.
    /// </summary>
    /// <param name="key">Metadata key. Keys are case-sensitive and should follow a consistent naming convention (e.g., "core.*", "plugin.&lt;id&gt;.*").</param>
    /// <param name="value">JSON value to store.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    public void SetMetadata(string key, JsonElement value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key cannot be null/empty.", nameof(key));

        var dict = EnsureMetadataLoadedMutable();
        dict[key] = value;
        PersistMetadata(dict);
    }

    /// <summary>
    /// Serializes the provided value and stores it as metadata under the specified key.
    /// </summary>
    /// <typeparam name="T">Value type to serialize.</typeparam>
    /// <param name="key">Metadata key. Keys are case-sensitive and should follow a consistent naming convention (e.g., "core.*", "plugin.&lt;id&gt;.*").</param>
    /// <param name="value">Value to serialize and store.</param>
    /// <remarks>
    /// Uses <see cref="JsonSerializer.SerializeToElement{TValue}(TValue, JsonSerializerOptions?)"/> to store the value as a <see cref="JsonElement"/>.
    /// </remarks>
    public void SetMetadata<T>(string key, T value)
        => SetMetadata(key, JsonSerializer.SerializeToElement(value));

    /// <summary>
    /// Removes a metadata key if present.
    /// </summary>
    /// <param name="key">Metadata key to remove.</param>
    /// <returns><see langword="true"/> if the key existed and was removed; otherwise <see langword="false"/>.</returns>
    public bool RemoveMetadata(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        var dict = EnsureMetadataLoadedMutable();
        if (!dict.Remove(key)) return false;

        PersistMetadata(dict);
        return true;
    }

    /// <summary>
    /// Ensures metadata is loaded and returns the current cached representation.
    /// </summary>
    /// <returns>A read-only view of the current metadata dictionary.</returns>
    private IReadOnlyDictionary<string, JsonElement> EnsureMetadataLoaded()
        => EnsureMetadataLoadedMutable();

    /// <summary>
    /// Ensures metadata is loaded into a mutable dictionary and returns it.
    /// </summary>
    /// <remarks>
    /// This method manages cache coherence between <see cref="MetadataJson"/> and the in-memory <see cref="_metadataCache"/>.
    /// </remarks>
    private Dictionary<string, JsonElement> EnsureMetadataLoadedMutable()
    {
        // If we have a cache and the JSON hasn't changed, we're good.
        if (_metadataCache is not null && _metadataJsonCache == MetadataJson)
            return _metadataCache;

        // If nothing stored, use empty.
        if (string.IsNullOrWhiteSpace(MetadataJson))
        {
            _metadataCache = _metadataCache ?? new Dictionary<string, JsonElement>();
            _metadataJsonCache = MetadataJson; // both null/whitespace
            return _metadataCache;
        }

        // Try to parse fresh.
        try
        {
            _metadataCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(MetadataJson)
                            ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            _metadataCache = new Dictionary<string, JsonElement>();
        }

        _metadataJsonCache = MetadataJson;
        return _metadataCache;
    }

    /// <summary>
    /// Persists the provided metadata dictionary into <see cref="MetadataJson"/> and updates internal caches.
    /// </summary>
    /// <param name="dict">Metadata dictionary to persist.</param>
    /// <remarks>
    /// Stores <see langword="null"/> when empty to avoid persisting an empty JSON object (<c>{}</c>).
    /// </remarks>
    private void PersistMetadata(Dictionary<string, JsonElement> dict)
    {
        // Optional: if empty, store NULL instead of "{}"
        if (dict.Count == 0)
        {
            MetadataJson = null;
            _metadataJsonCache = null;
            _metadataCache = dict;
            return;
        }

        MetadataJson = JsonSerializer.Serialize(dict);
        _metadataJsonCache = MetadataJson; // keep cache coherent
        _metadataCache = dict;
    }

    /// <summary>
    /// Represents a single barcode value and its symbology.
    /// </summary>
    /// <param name="Symbology">Barcode symbology/standard.</param>
    /// <param name="Code">Barcode payload (typically digits/characters, ideally normalized according to symbology rules).</param>
    public record BarcodeRecord(BarcodeSymbology Symbology, string Code);
}

/// <summary>
/// Barcode symbologies supported by OOS.
/// </summary>
public enum BarcodeSymbology
{
    /// <summary>
    /// Symbology is unknown or unspecified.
    /// </summary>
    Unknown,

    /// <summary>
    /// Universal Product Code (stored canonically as UPC-A digits).
    /// </summary>
    UPC,

    /// <summary>
    /// Code 128 (high density alphanumeric barcode).
    /// </summary>
    Code128,

    /// <summary>
    /// EAN-8 (8-digit European Article Number).
    /// </summary>
    EAN8,

    /// <summary>
    /// EAN-13 (13-digit European Article Number).
    /// </summary>
    EAN13
}