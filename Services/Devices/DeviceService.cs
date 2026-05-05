// OpenOrderSystem.Core/Services/Devices/DeviceService.cs
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Services.Devices.Attributes;
using OpenOrderSystem.Core.Services.Devices.Interfaces;

namespace OpenOrderSystem.Core.Services.Devices;

/// <summary>
/// Default implementation of <see cref="IDeviceService"/> responsible for authenticating
/// devices, resolving handlers, enforcing permissions, and dispatching commands to
/// registered OOS devices.
/// </summary>
/// <remarks>
/// <para>
/// Handlers are resolved via keyed dependency injection using the target device's
/// <see cref="Data.DataModels.V2.Devices.DeviceHead.DeviceType"/> as the service key.
/// All handler resolution, authentication, permission enforcement, and dispatch is
/// internal — callers interact only through <see cref="IDeviceService"/>.
/// </para>
/// <para>
/// Since this service is scoped, each request gets its own instance with its own
/// authentication state. Authentication state does not persist across requests.
/// </para>
/// </remarks>
public class DeviceService : IDeviceService
{
    private readonly IDbContextFactory<OosDbContext> _contextFactory;
    private readonly IKeyedServiceProvider _keyedServices;
    private readonly ILogger<DeviceService> _logger;

    /// <inheritdoc/>
    public DeviceContext? SenderContext { get; private set; }

    /// <inheritdoc/>
    public HashSet<string>? Permissions { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceService"/>.
    /// </summary>
    /// <param name="contextFactory">Factory for creating <see cref="OosDbContext"/> instances.</param>
    /// <param name="keyedServices">Keyed service provider used to resolve device handlers by type.</param>
    /// <param name="logger">Logger for this service.</param>
    public DeviceService(
        IDbContextFactory<OosDbContext> contextFactory,
        IKeyedServiceProvider keyedServices,
        ILogger<DeviceService> logger)
    {
        _contextFactory = contextFactory;
        _keyedServices = keyedServices;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IDeviceService> AuthenticateDevice(string deviceKey, string deviceSecret)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var device = await db.Devices
            .Include(d => d.Parent)
            .Include(d => d.Children)
            .Include(d => d.Config)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Key == deviceKey);

        if (device is null)
        {
            _logger.LogWarning("AuthenticateDevice failed: no device found with key '{DeviceKey}'.", deviceKey);
            return this;
        }

        if (!ValidateSecret(deviceSecret, device.Config))
        {
            _logger.LogWarning("AuthenticateDevice failed: invalid secret for device '{DeviceKey}'.", deviceKey);
            return this;
        }

        SenderContext = DeviceContext.Create(device);
        Permissions = ResolvePermissions(device.DeviceType, device.Config);

        return this;
    }

    /// <inheritdoc/>
    public async Task<IDeviceService> AuthenticateDevice(Guid deviceId, string deviceSecret)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var device = await db.Devices
            .Include(d => d.Parent)
            .Include(d => d.Children)
            .Include(d => d.Config)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device is null)
        {
            _logger.LogWarning("AuthenticateDevice failed: no device found with id '{DeviceId}'.", deviceId);
            return this;
        }

        if (!ValidateSecret(deviceSecret, device.Config))
        {
            _logger.LogWarning("AuthenticateDevice failed: invalid secret for device '{DeviceId}'.", deviceId);
            return this;
        }

        SenderContext = DeviceContext.Create(device);
        Permissions = ResolvePermissions(device.DeviceType, device.Config);

        return this;
    }

    /// <inheritdoc/>
    public async Task<DeviceCmdResult> ExecAsync(string deviceKey, string command, Dictionary<string, JsonElement> data)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var device = await db.Devices
            .Include(d => d.Parent)
            .Include(d => d.Children)
            .Include(d => d.Config)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Key == deviceKey);

        if (device is null)
        {
            _logger.LogWarning("ExecAsync failed: no device found with key '{DeviceKey}'.", deviceKey);
            return DeviceCmdResult.NotFound(deviceKey);
        }

        return await DispatchAsync(DeviceContext.Create(device), device.DeviceType, command, data);
    }

    /// <inheritdoc/>
    public async Task<DeviceCmdResult> ExecAsync(Guid deviceId, string command, Dictionary<string, JsonElement> data)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var device = await db.Devices
            .Include(d => d.Parent)
            .Include(d => d.Children)
            .Include(d => d.Config)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device is null)
        {
            _logger.LogWarning("ExecAsync failed: no device found with id '{DeviceId}'.", deviceId);
            return DeviceCmdResult.NotFound(deviceId.ToString());
        }

        return await DispatchAsync(DeviceContext.Create(device), device.DeviceType, command, data);
    }

    /// <summary>
    /// Resolves the handler for the given device type, enforces authentication and
    /// permission requirements, and dispatches the command.
    /// </summary>
    /// <param name="device">The target device context.</param>
    /// <param name="deviceType">The device type discriminator used to resolve the handler.</param>
    /// <param name="command">The command name to execute.</param>
    /// <param name="data">A dictionary of named parameters for the command.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> describing the outcome of the command.</returns>
    private async Task<DeviceCmdResult> DispatchAsync(DeviceContext device, string deviceType, string command, Dictionary<string, JsonElement> data)
    {
        var handler = _keyedServices.GetKeyedService<IDeviceHandler>(deviceType);

        if (handler is null)
        {
            _logger.LogWarning(
                "ExecAsync failed: no handler registered for device type '{DeviceType}'.", deviceType);
            return DeviceCmdResult.NoHandler(deviceType);
        }

        // Find the target method for permission and auth checks
        var targetMethod = handler.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.GetCustomAttributes<DeviceExecTargetAttribute>()
                .Any(a => string.Equals(a.Command, command, StringComparison.OrdinalIgnoreCase)));

        if (targetMethod is not null)
        {
            // Protected methods require authentication
            if (targetMethod.IsFamily && ((IDeviceService)this).IsAuthenticated())
            {
                _logger.LogWarning(
                    "ExecAsync failed: command '{Command}' on device type '{DeviceType}' requires authentication.",
                    command, deviceType);
                return DeviceCmdResult.Fail($"Command '{command}' requires authentication.");
            }

            // Check permission requirements if authenticated
            if (((IDeviceService)this).IsAuthenticated())
            {
                var permissionAttr = targetMethod.GetCustomAttribute<DeviceCommandPermissionAttribute>();
                if (permissionAttr is not null)
                {
                    var required = permissionAttr.Required
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    var authorized = permissionAttr.Type == PermissionType.RequireAll
                        ? required.All(p => Permissions!.Contains(p))
                        : required.Any(p => Permissions!.Contains(p));

                    if (!authorized)
                    {
                        _logger.LogWarning(
                            "ExecAsync failed: device '{SenderKey}' lacks required permissions for command '{Command}' on device type '{DeviceType}'.",
                            SenderContext!.Key, command, deviceType);
                        return DeviceCmdResult.Fail(
                            $"Device '{SenderContext!.Key}' lacks required permissions for command '{command}'.");
                    }
                }
            }
        }

        try
        {
            return await handler.ExecAsync(device, command, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception dispatching command '{Command}' to device type '{DeviceType}'.",
                command, deviceType);
            return DeviceCmdResult.FromException(command, deviceType, ex);
        }
    }

    /// <summary>
    /// Validates the provided raw secret against the SHA512 hash stored in the device's
    /// configuration under the key <c>auth.secret</c>.
    /// </summary>
    /// <param name="secret">The raw secret provided by the device.</param>
    /// <param name="config">The device's configuration entries.</param>
    /// <returns><see langword="true"/> if the secret is valid; otherwise <see langword="false"/>.</returns>
    private static bool ValidateSecret(string secret, IEnumerable<Data.DataModels.V2.Devices.DeviceConfig> config)
    {
        var storedHash = config.FirstOrDefault(c => c.Key == "auth.secret")?.Value;
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        var parts = storedHash.Split(':');
        if (parts.Length != 2)
            return false;

        var salt = parts[1];
        var hash = HashSecret(secret, salt);

        return hash == storedHash;
    }

    /// <summary>
    /// Computes a SHA512 hash of the provided secret combined with the given salt.
    /// </summary>
    /// <param name="secret">The raw secret to hash.</param>
    /// <param name="salt">The salt to combine with the secret.</param>
    /// <returns>A colon-separated string of <c>{hash}:{salt}</c>.</returns>
    private static string HashSecret(string secret, string salt)
    {
        using var sha512 = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(secret + salt);
        var hash = sha512.ComputeHash(bytes);
        return $"{Convert.ToHexString(hash)}:{salt}";
    }

    /// <summary>
    /// Resolves the effective permission set for an authenticated device by combining
    /// type-level defaults from <see cref="DevicePermissionAttribute"/> with instance-level
    /// grants and revocations from <see cref="Data.DataModels.V2.Devices.DeviceConfig"/>.
    /// </summary>
    /// <param name="deviceType">The device type used to locate the handler and read type-level permissions.</param>
    /// <param name="config">The device's configuration entries.</param>
    /// <returns>A <see cref="HashSet{T}"/> of resolved permission strings.</returns>
    private HashSet<string> ResolvePermissions(string deviceType, IEnumerable<Data.DataModels.V2.Devices.DeviceConfig> config)
    {
        var handler = _keyedServices.GetKeyedService<IDeviceHandler>(deviceType);

        // Start with type-level defaults from DevicePermissionAttribute
        var permissions = handler is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : handler.GetType()
                .GetCustomAttributes<DevicePermissionAttribute>()
                .Select(a => a.Permission)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var configList = config.ToList();

        // Apply instance-level grants
        var grants = configList.FirstOrDefault(c => c.Key == "device.permissions.grant")?.Value;
        if (!string.IsNullOrWhiteSpace(grants))
        {
            foreach (var permission in grants.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                permissions.Add(permission);
        }

        // Apply instance-level revocations — gracefully degrades if permission doesn't exist
        var revocations = configList.FirstOrDefault(c => c.Key == "device.permissions.revoke")?.Value;
        if (!string.IsNullOrWhiteSpace(revocations))
        {
            foreach (var permission in revocations.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                permissions.Remove(permission);
        }

        return permissions;
    }
}