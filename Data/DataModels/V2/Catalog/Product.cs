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
    public Guid? GroupId { get; set;}
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