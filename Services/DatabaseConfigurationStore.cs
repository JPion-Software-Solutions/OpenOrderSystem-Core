using System;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.Interfaces;
using OpenOrderSystem.Core.Services.Interfaces;

namespace OpenOrderSystem.Core.Services;

/// <summary>
/// Intended to be the eventual replacement for the original configuration service.
/// </summary>
public class DatabaseConfigurationStore<TContext> : IConfigurationStore where TContext: IConfigurationStoreContext
{
    private readonly TContext _context;
    private readonly UserStore<IdentityUser> _userStore;
    private readonly ILogger<DatabaseConfigurationStore<TContext>> _logger;

    public DatabaseConfigurationStore(TContext context, UserStore<IdentityUser> userStore, ILogger<Services.DatabaseConfigurationStore<TContext>> logger)
    {
        _context = context;
        _userStore = userStore;
        _logger = logger;
    }

    public async Task<T?> GetConfigurationAsync<T>(string key, T? defaultValue = default)
    {
        var row = await _context.Confguration
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key);

        if (row?.Value is null)
            return defaultValue;

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

    public async Task<string?> GetConfigurationAsync(string key, string? defaultValue = null) =>
        (await _context.Confguration
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key))?.Value ?? defaultValue;

    public async Task<SystemConfig?> SetConfigurationAsync(string key, object value, IConfigurationStore.SetConfigOptions? options = null)
    {
        var row = await _context.Confguration.FirstOrDefaultAsync(c => c.Key == key);

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
                IsLocked = options?.setLocked ?? false
            };
        }
        else
        {
            row.Value = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            row.IsLocked = options?.setLocked ?? false;
        }

        _context.Confguration.Update(row);
        await _context.SaveChangesAsync();

        return row;
    }
}
