using System;
using OpenOrderSystem.Core.Data.DataModels.V2.Core;

namespace OpenOrderSystem.Core.Services.Interfaces;

public interface IConfigurationStore
{
    public Task<T?> GetConfigurationAsync<T>(string key, T? defaultValue = default);

    public Task<string?> GetConfigurationAsync(string key, string? defaultValue = null);

    public Task<SystemConfig?> SetConfigurationAsync(string key, object value, SetConfigOptions? options = null);

    public sealed record SetConfigOptions(
        string? ActorId = null,
        bool forceOverwrite = false,
        string? reason = null,
        bool setLocked = false
    );
}