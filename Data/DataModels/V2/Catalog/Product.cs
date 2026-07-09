using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

/// <summary>
/// Represents a catalog product (a conceptual item in the menu/catalog).
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Product"/> is not necessarily directly purchasable. In an ecommerce-friendly model,
/// the purchasable unit is typically a <see cref="Variant"/> (SKU-priced sellable configuration).
/// </para>
/// <para>
/// This entity supports extensibility via <see cref="MetadataJson"/>. Contributors should avoid writing
/// to <see cref="MetadataJson"/> directly; prefer <see cref="SetMetadata(string, JsonElement)"/>,
/// <see cref="SetMetadata{T}(string, T)"/>, and <see cref="RemoveMetadata(string)"/> to keep internal caches coherent.
/// </para>
/// </remarks>
public class Product : IGroupMember<ProductGroup, Product>
{
    private Dictionary<string, JsonElement>? _metadataCache;
    private string? _metadataJsonCache;

    /// <summary>
    /// Unique identifier for this product.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name of the product (e.g., "Pepperoni Pizza", "Coke").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional customer-facing description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional search keywords/tags used to improve discoverability.
    /// </summary>
    /// <remarks>
    /// This can be a simple space/comma-delimited list for now. If you later add a real search index,
    /// this field can become derived/optional.
    /// </remarks>
    public string? Keywords { get; set; }

    /// <summary>
    /// Optional reference to the media asset used as the product's primary/cover image.
    /// </summary>
    /// <remarks>
    /// This is the default media shown for the product in catalog listings and detail views when a single representative
    /// image is needed. This reference is part of the live catalog state and may change over time.
    /// </remarks>
    public Guid? CoverMediaId { get; set; }

    /// <summary>
    /// Navigation to the media asset used as the product's primary/cover image.
    /// </summary>
    /// <remarks>
    /// Optional. When set, this should correspond to <see cref="CoverMediaId"/>.
    /// </remarks>
    public Media? CoverMedia { get; set; }

    /// <summary>
    /// Optional reference to a <see cref="MediaGroup"/> that serves as this product's media album.
    /// </summary>
    /// <remarks>
    /// The album represents a collection of related media assets (e.g., additional photos, videos, documents) associated
    /// with the product. This is part of the live catalog state and may change over time.
    /// </remarks>
    public Guid? AlbumId { get; set; }

    /// <summary>
    /// Navigation to the <see cref="MediaGroup"/> that serves as this product's media album.
    /// </summary>
    /// <remarks>
    /// Optional. When set, this should correspond to <see cref="AlbumId"/>.
    /// </remarks>
    public MediaGroup? Album { get; set; }

    /// <summary>
    /// JSON storage for <see cref="Metadata"/>.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> Writing to this property directly is not recommended.
    /// Use metadata helper methods to keep cached state coherent and to avoid persisting invalid JSON.
    /// </remarks>
    public string? MetadataJson { get; set; }
    
    
    /// <summary>
    /// Immutable historical snapshots of this product's graph, ordered by creation time.
    /// The most recent snapshot reflects the current state of the product at the time it was last snapshotted.
    /// Referenced by <see cref="OpenOrderSystem.Core.Data.DataModels.V2.Ordering.OrderLine"/> records
    /// to preserve the product state as it existed when an order was placed.
    /// </summary>
    public List<ProductSnapshot>? Snapshots { get; set; }

    /// <summary>
    /// Behavioral and display flags for this product.
    /// </summary>
    /// <seealso cref="ProductFlags"/>
    public ProductFlags Flags { get; set; } = ProductFlags.None;
    
    /// <summary>
    /// Arbitrary extension metadata for this product.
    /// </summary>
    /// <remarks>
    /// Metadata is intended for non-core extension fields (plugin/vendor specific data, UI hints, etc.).
    /// Values that require querying, indexing, or core validation should be promoted to first-class columns.
    /// </remarks>
    [NotMapped]
    public IReadOnlyDictionary<string, JsonElement> Metadata => EnsureMetadataLoaded();

    /// <summary>
    /// Variants belonging to this product (sellable configurations).
    /// </summary>
    public List<Variant> Variants { get; set; } = new();
    /// <summary>
    /// Optional foreign key to the <see cref="ProductGroup"/> this product belongs to.
    /// </summary>
    public Guid? GroupId { get; set;}

    /// <summary>
    /// Navigation property for the <see cref="ProductGroup"/> this product belongs to.
    /// </summary>
    public ProductGroup? Group { get; set;}

    /// <summary>
    /// Sets a metadata key to the specified JSON value.
    /// </summary>
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
    public void SetMetadata<T>(string key, T value)
        => SetMetadata(key, JsonSerializer.SerializeToElement(value));

    /// <summary>
    /// Removes a metadata key if present.
    /// </summary>
    public bool RemoveMetadata(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        var dict = EnsureMetadataLoadedMutable();
        if (!dict.Remove(key)) return false;

        PersistMetadata(dict);
        return true;
    }

    private IReadOnlyDictionary<string, JsonElement> EnsureMetadataLoaded()
        => EnsureMetadataLoadedMutable();

    private Dictionary<string, JsonElement> EnsureMetadataLoadedMutable()
    {
        if (_metadataCache is not null && _metadataJsonCache == MetadataJson)
            return _metadataCache;

        if (string.IsNullOrWhiteSpace(MetadataJson))
        {
            _metadataCache = _metadataCache ?? new Dictionary<string, JsonElement>();
            _metadataJsonCache = MetadataJson;
            return _metadataCache;
        }

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

    private void PersistMetadata(Dictionary<string, JsonElement> dict)
    {
        if (dict.Count == 0)
        {
            MetadataJson = null;
            _metadataJsonCache = null;
            _metadataCache = dict;
            return;
        }

        MetadataJson = JsonSerializer.Serialize(dict);
        _metadataJsonCache = MetadataJson;
        _metadataCache = dict;
    }
}

/// <summary>
/// Behavioral and display flags for a <see cref="Product"/>, controlling availability,
/// visibility, promotional state, and extensibility via operator-configurable and
/// addon-reserved flag slots.
/// </summary>
[Flags]
public enum ProductFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates the product is currently out of stock and cannot be ordered.
    /// Distinct from variant-level availability.
    /// </summary>
    OutOfStock = 1 << 0,

    /// <summary>
    /// Hides the product from customer-facing views entirely.
    /// Distinct from <see cref="OutOfStock"/> which may still surface the product as unavailable.
    /// </summary>
    Hidden = 1 << 1,

    /// <summary>
    /// Marks the product as editorially highlighted by the operator (e.g., seasonal item,
    /// staff pick, new addition). Set and cleared manually by the operator.
    /// </summary>
    IsHighlighted = 1 << 2,

    /// <summary>
    /// Indicates an active promotion is running on this product. Typically set and cleared
    /// programmatically by the promotional service rather than manually by the operator.
    /// </summary>
    IsPromoted = 1 << 3,

    // ----------------------------
    // Operator Custom Flags (1–6)
    // ----------------------------

    /// <summary>
    /// Operator-configurable flag. Semantics are defined in system configuration.
    /// </summary>
    Custom1 = 1 << 7,

    /// <summary>
    /// Operator-configurable flag. Semantics are defined in system configuration.
    /// </summary>
    Custom2 = 1 << 8,

    /// <summary>
    /// Operator-configurable flag. Semantics are defined in system configuration.
    /// </summary>
    Custom3 = 1 << 9,

    /// <summary>
    /// Operator-configurable flag. Semantics are defined in system configuration.
    /// </summary>
    Custom4 = 1 << 10,

    /// <summary>
    /// Operator-configurable flag. Semantics are defined in system configuration.
    /// </summary>
    Custom5 = 1 << 11,

    /// <summary>
    /// Operator-configurable flag. Semantics are defined in system configuration.
    /// </summary>
    Custom6 = 1 << 12,

    // ----------------------------
    // Addon Reserved Flags (1–6)
    // ----------------------------

    /// <summary>
    /// Reserved for plugin and integration use. When set, indicates that addon-specific
    /// metadata is present on this product. Plugins should check their namespaced metadata
    /// keys before acting. See addon development documentation for key namespacing conventions.
    /// </summary>
    Addon1 = 1 << 15,

    /// <summary>
    /// Reserved for plugin and integration use. When set, indicates that addon-specific
    /// metadata is present on this product. Plugins should check their namespaced metadata
    /// keys before acting. See addon development documentation for key namespacing conventions.
    /// </summary>
    Addon2 = 1 << 16,

    /// <summary>
    /// Reserved for plugin and integration use. When set, indicates that addon-specific
    /// metadata is present on this product. Plugins should check their namespaced metadata
    /// keys before acting. See addon development documentation for key namespacing conventions.
    /// </summary>
    Addon3 = 1 << 17,

    /// <summary>
    /// Reserved for plugin and integration use. When set, indicates that addon-specific
    /// metadata is present on this product. Plugins should check their namespaced metadata
    /// keys before acting. See addon development documentation for key namespacing conventions.
    /// </summary>
    Addon4 = 1 << 18,

    /// <summary>
    /// Reserved for plugin and integration use. When set, indicates that addon-specific
    /// metadata is present on this product. Plugins should check their namespaced metadata
    /// keys before acting. See addon development documentation for key namespacing conventions.
    /// </summary>
    Addon5 = 1 << 19,

    /// <summary>
    /// Reserved for plugin and integration use. When set, indicates that addon-specific
    /// metadata is present on this product. Plugins should check their namespaced metadata
    /// keys before acting. See addon development documentation for key namespacing conventions.
    /// </summary>
    Addon6 = 1 << 20,
}