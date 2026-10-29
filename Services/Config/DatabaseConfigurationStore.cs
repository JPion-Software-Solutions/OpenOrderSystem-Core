using System;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2.Core;
using OpenOrderSystem.Core.Data.Interfaces;
using OpenOrderSystem.Core.Services.Config.Interfaces;

namespace OpenOrderSystem.Core.Services.Config;

/// <summary>
/// Intended to be the eventual replacement for the original configuration service.
/// Also implements <see cref="ITimeNormalizationService"/>: the site's configured local
/// time zone is just another entry in this same key-value store, under
/// <see cref="TIMEZONE_KEY"/>, so the two responsibilities share this class rather than
/// needing a second round trip through a separate time-zone-specific store.
/// </summary>
public class DatabaseConfigurationStore<TContext> : IConfigurationStore, ITimeNormalizationService where TContext: IConfigurationStoreContext
{
    private class CachedKey
    {
        private const int CACHE_TIMEOUT = 2;
        public string Key { get; init; }
        public string Value { get; init; }
        public bool IsLocked { get; init; }
        public DateTime Timestamp { get; init; }
        public DateTime ExpiresAt { get; init; }
        
        public  CachedKey(SystemConfig config)
        {
            Key = config.Key;
            Value = config.Value;
            IsLocked = config.IsLocked;
            Timestamp = config.Timestamp;
            ExpiresAt = DateTime.UtcNow.AddMinutes(CACHE_TIMEOUT);
        }
    }
    
    /// <summary>Configuration key under which the site's configured local time zone ID is stored.</summary>
    private readonly string TIMEZONE_KEY = "local_time_zone";
    private readonly TContext _context;
    private readonly ILogger<DatabaseConfigurationStore<TContext>> _logger;

    // Static + ConcurrentDictionary: one cache shared across every request/scope for the
    // life of the process (config is site-wide, not per-request), safe under concurrent
    // reads and writes from multiple simultaneous requests.
    private static readonly ConcurrentDictionary<string, CachedKey> _cachedKeys = new();

    public DatabaseConfigurationStore(TContext context, ILogger<DatabaseConfigurationStore<TContext>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<T?> GetConfigurationAsync<T>(string key, T? defaultValue = default)
    {
        if (_cachedKeys.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            var cachedValue = JsonSerializer.Deserialize<T>(cached.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return cachedValue is null ? defaultValue : cachedValue;
        }

        var row = await _context.Configuration
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key);

        if (row?.Value is null)
            return defaultValue;

        _cachedKeys[key] = new CachedKey(row);

        try
        {
            var value = JsonSerializer.Deserialize<T>(row.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return value is null ? defaultValue : value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize configuration key '{Key}' to type '{Type}'. Returning default.",
                key, typeof(T).FullName);

            return defaultValue;
        }
    }

    public async Task<string?> GetConfigurationAsync(string key, string? defaultValue = null)
    {
        if (_cachedKeys.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Value;

        var row = await _context.Configuration
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key);

        if (row is null)
            return defaultValue;

        _cachedKeys[key] = new CachedKey(row);

        return row.Value;
    }

    public async Task<SystemConfig?> SetConfigurationAsync(string key, object value, IConfigurationStore.SetConfigOptions? options = null)
    {
        var row = await _context.Configuration.FirstOrDefaultAsync(c => c.Key == key);

        if (row is not null && row.IsLocked && options?.forceOverwrite != true)
        {
            _logger.LogError($"Cannot set key: {key}. Key stored as locked, please call with options.forceOverwrite to overwrite value.");
            return null;
        }

        if (row is null)
        {
            row = new SystemConfig
            {
                Key = key,
                Value = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                IsLocked = options?.setLocked ?? false,
                Timestamp = DateTime.UtcNow
            };
        }
        else
        {
            row.Value = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            row.Timestamp = DateTime.UtcNow;
            row.IsLocked = options?.setLocked ?? false;
        }

        _context.Configuration.Update(row);
        await _context.SaveChangesAsync();

        // Refresh (not just evict) so a read immediately after this write sees the new
        // value right away, instead of possibly serving the old cached value for up to
        // CachedKey's full timeout.
        _cachedKeys[key] = new CachedKey(row);

        return row;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Hits the configuration table on every call (via <see cref="GetLocalTimezoneId"/>) —
    /// there is no in-memory caching of the resolved <see cref="TimeZoneInfo"/>, so this is
    /// not free to call from a hot path like a per-request view render.
    /// </remarks>
    public async Task<DateTimeOffset> GetLocalTime()
    {
        var timezoneId = await GetLocalTimezoneId();
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);

        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone);
    }

    /// <inheritdoc />
    // Typed overload: SetLocalTimezone stores the ID JSON-serialized (quoted), so the raw string
    // overload would hand FindSystemTimeZoneById a value with the quotes still on.
    public async Task<string> GetLocalTimezoneId() =>
        await GetConfigurationAsync<string>(TIMEZONE_KEY, TimeZoneInfo.Utc.Id) ?? TimeZoneInfo.Utc.Id;

    /// <inheritdoc />
    public async Task<DateTimeOffset> ConvertToLocalTime(DateTimeOffset date)
    {
        var timezoneId = await GetLocalTimezoneId();
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);

        return TimeZoneInfo.ConvertTime(date, timezone);
    }

    /// <inheritdoc />
    public DateTimeOffset ConvertToUTC(DateTimeOffset date) => TimeZoneInfo.ConvertTime(date, TimeZoneInfo.Utc);

    /// <inheritdoc />
    public async Task SetLocalTimezone(string ianaTimeZone)
    {
        // Validate up front so a bad ID throws here, at the one call site that can reject
        // it, rather than silently saving and crashing every later read of local time.
        // Throws TimeZoneNotFoundException / InvalidTimeZoneException if unresolvable.
        TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZone);

        var result = await SetConfigurationAsync(TIMEZONE_KEY, ianaTimeZone);

        // SetConfigurationAsync returns null only when the key is locked against writes.
        // There's no supported UI path to lock this key, so this should only ever fire
        // from a direct DB edit or a future config-store admin tool — but if it does,
        // fail loud rather than silently discarding the write.
        if (result is null)
        {
            throw new InvalidOperationException(
                $"Failed to set the local time zone: configuration key '{TIMEZONE_KEY}' is locked.");
        }
    }
}
