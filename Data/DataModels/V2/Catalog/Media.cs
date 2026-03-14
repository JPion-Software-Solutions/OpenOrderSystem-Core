using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

/// <summary>
/// Represents a media asset (image/audio/video/file) persisted by the catalog.
/// </summary>
/// <remarks>
/// <para>
/// This model stores core, query-worthy fields as columns (e.g., <see cref="MediaType"/>, <see cref="MimeType"/>,
/// <see cref="SizeBytes"/>, <see cref="Hash"/>), and supports arbitrary extension fields via <see cref="MetadataJson"/>.
/// </para>
/// <para>
/// This model uses a JSON-backed extension field (<see cref="MetadataJson"/>) with cached deserialization to avoid
/// repeatedly parsing JSON on every access.
/// </para>
/// <para>
/// <b>Important:</b> Avoid writing to <see cref="MetadataJson"/> directly in application code.
/// Doing so bypasses cache coherence conventions and can result in stale in-memory values and/or invalid JSON being persisted.
/// Prefer using metadata helper methods (<see cref="SetMetadata(string, JsonElement)"/>, <see cref="SetMetadata{T}(string, T)"/>,
/// <see cref="RemoveMetadata(string)"/>).
/// </para>
/// </remarks>
public class Media : IGroupMember<MediaGroup, Media>
{
    private Dictionary<string, JsonElement>? _metadataCache;
    private string? _metadataJsonCache;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-friendly name for this media item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description or notes for this media item.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Storage locator (v1: simple filesystem path; may be replaced by MediaForge in the future).
    /// </summary>
    public string Filepath { get; set; } = string.Empty;

    /// <summary>
    /// Original filename as provided by the source (upload/import), if known.
    /// </summary>
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// File extension.
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// MIME type string (e.g. "image/png", "video/mp4").
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// High-level + specific media classification (used for filtering/querying).
    /// </summary>
    public MediaType MediaType { get; set; } = MediaType.Unsupported;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// File integrity hash (recommended: canonical hex SHA-256).
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Row creation time (UTC).
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last update time (UTC).
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// JSON storage for <see cref="Metadata"/>.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> Writing to this property directly is not recommended.
    /// Prefer using metadata helper methods to keep cached state coherent and to avoid persisting invalid JSON.
    /// </remarks>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Arbitrary extension metadata for this media item.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is not mapped by EF Core. It is backed by <see cref="MetadataJson"/> and uses cached deserialization.
    /// </para>
    /// <para>
    /// Metadata is intended for non-core, non-indexed extension fields (e.g., image dimensions, EXIF info, codec details,
    /// plugin/vendor values). If a value must be queried, indexed, or validated as part of core business rules, it should
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

    public Guid? GroupId { get; set; }
    public MediaGroup? Group { get; set; }

    public void SetMetadata(string key, JsonElement value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key cannot be null/empty.", nameof(key));

        var dict = EnsureMetadataLoadedMutable();
        dict[key] = value;
        PersistMetadata(dict);
    }

    public void SetMetadata<T>(string key, T value)
        => SetMetadata(key, JsonSerializer.SerializeToElement(value));

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

public enum MediaType
{
    Unsupported = 0,

    // ----------------------------
    // Images (100–199)
    // ----------------------------
    Image = 100,
    Image_Jpg  = 101,
    Image_Png  = 102,
    Image_Gif  = 103,
    Image_Tiff = 104,
    Image_Bmp  = 105,
    Image_Webp = 106,
    Image_Svg  = 107,
    Image_Avif = 108,
    Image_Heic = 109,
    Image_Ico  = 110,

    /// <summary>
    /// Sentinel value used for range filtering (Image = 100..199).
    /// WARNING: This is NOT a real media type and must never be stored on a Media record.
    /// </summary>
    IMAGE_STOP = 199,

    // ----------------------------
    // Audio (200–299)
    // ----------------------------
    Audio = 200,
    Audio_Mp3  = 201,
    Audio_Wav  = 202,
    Audio_Ogg  = 203,
    Audio_Flac = 204,
    Audio_Aac  = 205,
    Audio_M4a  = 206,
    Audio_Opus = 207,

    /// <summary>
    /// Sentinel value used for range filtering (Audio = 200..299).
    /// WARNING: This is NOT a real media type and must never be stored on a Media record.
    /// </summary>
    AUDIO_STOP = 299,

    // ----------------------------
    // Video (300–399)
    // ----------------------------
    Video = 300,
    Video_Mp4  = 301,
    Video_Webm = 302,
    Video_Mov  = 303,
    Video_Mkv  = 304,
    Video_Avi  = 305,

    /// <summary>
    /// Sentinel value used for range filtering (Video = 300..399).
    /// WARNING: This is NOT a real media type and must never be stored on a Media record.
    /// </summary>
    VIDEO_STOP = 399,

    // ----------------------------
    // Files (400–499)
    // ----------------------------
    File = 400,
    File_Pdf  = 401,
    File_Txt  = 402,
    File_Csv  = 403,
    File_Json = 404,
    File_Xml  = 405,
    File_Zip  = 420,

    /// <summary>
    /// Sentinel value used for range filtering (File = 400..499).
    /// WARNING: This is NOT a real media type and must never be stored on a Media record.
    /// </summary>
    FILE_STOP = 499,
}