using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using OpenOrderSystem.Core.Services.Config.Interfaces;

namespace OpenOrderSystem.Core.API.V2.System;

public static class PingUtility
{
    private static bool _isLocked = false;
    private static long _startTimestamp;
    private static readonly Dictionary<string, Func<IServiceProvider, Task<object?>>> _health = new();
    private static readonly Dictionary<string, Func<IServiceProvider, Task<object?>>> _data = new Dictionary<string, Func<IServiceProvider, Task<object?>>>
    {
        {
            "version",
            _ => Value(Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString() ?? "unknown")
        },
        {
            "Store_Name",
            async services => services.GetService<IConfigurationStore>() is { } store
                ? await store.GetConfigurationAsync("StoreName", "unknown")
                : "unknown"
        },
        {
            "Uptime",
            _ => Value(Stopwatch.GetElapsedTime(_startTimestamp).ToString("c"))
        },
        {
            "Uptime_Milliseconds",
            _ => Value(Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds)
        },
        {
            "Health_Checks",
            async services  =>
            {
                var data = new Dictionary<string, object?>();

                foreach (var (key, func) in _health)
                {
                    try
                    {
                        data[key] = await func(services);
                    }
                    catch (Exception ex)
                    {
                        data[key] = $"error: {ex.GetType().Name}";
                    }
                }

                return data.Keys.Any() ? data : "No monitored services found";
            }
        }
    };

    public static Task<object?> Value(object? value) => Task.FromResult(value);

    public static bool RegisterData(string key, Func<IServiceProvider, Task<object?>> func)
    {
        if (!_isLocked) _data[key] = func;
        return !_isLocked;
    }

    public static void Lock()
    {
        _startTimestamp = Stopwatch.GetTimestamp();
        _isLocked = true;
    }

    public static async Task<IResult> Get(IServiceProvider serviceProvider)
    {
        var data = new Dictionary<string, object?>();

        foreach (var (key, func) in _data)
        {
            try
            {
                data[key] = await func(serviceProvider);
            }
            catch (Exception ex)
            {
                data[key] = $"error: {ex.GetType().Name}";
            }
        }

        return Results.Json(data);
    }
}
