using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenOrderSystem.Core.Data.DataModels.V2.EventBus;
using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;
using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

/// <summary>
/// The event bus service group: everything that turns system events into rule actions and runs them.
/// </summary>
/// <remarks>
/// <para>
/// Depends on the database group (<see cref="DatabaseBuilderExtensions.AddOosDatabaseServices"/>):
/// <see cref="IEventRuleValidationService"/> reads devices through <see cref="Data.DataModels.V2.OosDbContext"/>.
/// It does not depend on the device group and can be added before or after it.
/// </para>
/// <para>
/// The ordering group (<see cref="OrderingBuilderExtensions.AddOosOrderingServices"/>) depends on this
/// group: order placement raises <c>order.placed</c> through <see cref="ISystemEventBus"/>.
/// </para>
/// </remarks>
public static class EventBusBuilderExtensions
{
    /// <summary>
    /// Registers the event bus service group. Safe to call more than once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Today this registers:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// The singleton <see cref="ISystemEventCatalog"/> of system events, found by scanning every loaded
    /// assembly (see <see cref="AddSystemEvents"/>).
    /// </item>
    /// <item>
    /// The singleton <see cref="ISystemEventBus"/>, which raises events into the outbox of the caller's
    /// context.
    /// </item>
    /// <item>
    /// The rule action handlers and the <see cref="IRuleActionCatalog"/> that describes them, found the
    /// same way (see <see cref="AddRuleActionHandlers"/>).
    /// </item>
    /// <item>The scoped <see cref="IEventRuleValidationService"/>.</item>
    /// </list>
    /// <para>
    /// The rest of the group (the dispatcher, the action queue and its worker) joins it here as it is
    /// built.
    /// </para>
    /// <para>
    /// Only assemblies already loaded into the app domain are scanned, so load plugin assemblies before
    /// calling this. Each scan reports its own problems in one exception, and the event scan runs first. Handlers resolve their own dependencies (device actions need the device job queue),
    /// so a boot mode using this group must also register those, and the database group for rule
    /// validation.
    /// </para>
    /// </remarks>
    /// <param name="bob">The web application builder.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if system event or rule action registration fails. See <see cref="AddSystemEvents"/> and
    /// <see cref="AddRuleActionHandlers"/>.
    /// </exception>
    public static WebApplicationBuilder AddOosEventBusServices(this WebApplicationBuilder bob)
    {
        ArgumentNullException.ThrowIfNull(bob);

        AddSystemEvents(bob.Services);
        bob.Services.TryAddSingleton<ISystemEventBus, OutboxSystemEventBus>();
        AddRuleActionHandlers(bob.Services);
        bob.Services.TryAddScoped<IEventRuleValidationService, DbEventRuleValidationService>();

        return bob;
    }

    /// <summary>
    /// Scans every loaded assembly that can contain system events and registers the singleton
    /// <see cref="ISystemEventCatalog"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every non-abstract class with <see cref="SystemEventAttribute"/> is added to the catalog. Event
    /// types are not registered in DI; they are records created by the code that raises them. Keys must
    /// be unique across all assemblies and fit in <see cref="OutboxEvent.EventKey"/>.
    /// </para>
    /// <para>
    /// Only this assembly and assemblies that reference it are scanned (see
    /// <see cref="AttributedTypeScanner.FindAttributed{TAttribute}"/>). Does nothing if the
    /// catalog is already registered.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown, listing every problem and the types and assemblies involved, if an assembly's types
    /// cannot be loaded, two events share a key, a key is blank or longer than
    /// <see cref="OutboxEvent.EventKeyMaxLength"/> characters, or a class with the attribute does not
    /// implement <see cref="ISystemEvent"/> or is an open generic type. Nothing is registered when this
    /// is thrown.
    /// </exception>
    private static void AddSystemEvents(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(ISystemEventCatalog))) return;

        var errors = new List<string>();
        var found = AttributedTypeScanner.FindAttributed<SystemEventAttribute>(
            typeof(ISystemEvent), a => a.Key, "System event", errors);

        foreach (var (type, attribute) in found)
        {
            if (attribute.Key is { Length: > OutboxEvent.EventKeyMaxLength })
                errors.Add($"{AttributedTypeScanner.Describe(type)} has [SystemEvent] with a key of {attribute.Key.Length} characters, but keys may be at most {OutboxEvent.EventKeyMaxLength} to fit {nameof(OutboxEvent)}.{nameof(OutboxEvent.EventKey)}.");
        }

        AttributedTypeScanner.ThrowIfAny(errors, "System event registration failed:");

        var descriptors = found.Select(f => new SystemEventDescriptor(
            f.Attribute.Key,
            f.Type,
            string.IsNullOrWhiteSpace(f.Attribute.DisplayName) ? f.Attribute.Key : f.Attribute.DisplayName,
            f.Attribute.Description));

        services.AddSingleton<ISystemEventCatalog>(new SystemEventCatalog(descriptors));
    }

    /// <summary>
    /// Scans every loaded assembly that can contain rule actions, registers each handler as a keyed
    /// scoped <see cref="IRuleActionHandler"/>, and registers the singleton
    /// <see cref="IRuleActionCatalog"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every non-abstract class with <see cref="RuleActionAttribute"/> is registered with
    /// <c>AddKeyedScoped(typeof(IRuleActionHandler), key, type)</c>, so the action worker resolves a
    /// handler by its action key. Keys must be unique across all assemblies.
    /// </para>
    /// <para>
    /// Only this assembly and assemblies that reference it are scanned (see
    /// <see cref="AttributedTypeScanner.FindAttributed{TAttribute}"/>). Does nothing if the
    /// catalog is already registered.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown, listing every problem and the types and assemblies involved, if an assembly's types
    /// cannot be loaded, two handlers share a key, a class with the attribute does not implement
    /// <see cref="IRuleActionHandler"/> or is an open generic type, a key is blank, a device action does
    /// not set <see cref="RuleActionAttribute.TargetDeviceType"/>, a service action does set it, a
    /// <see cref="RuleActionAttribute.TargetDeviceType"/> is not a device type definition, or a
    /// <see cref="RuleActionAttribute.SettingsType"/> is not a non-abstract class or record or cannot be
    /// deserialized with <see cref="RuleActionSettings.SettingsOptions"/>. Nothing is registered when this
    /// is thrown.
    /// </exception>
    private static void AddRuleActionHandlers(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IRuleActionCatalog))) return;

        var errors = new List<string>();
        var found = AttributedTypeScanner.FindAttributed<RuleActionAttribute>(
            typeof(IRuleActionHandler), a => a.Key, "Rule action", errors);

        var descriptors = new List<RuleActionDescriptor>(found.Count);
        foreach (var (type, attribute) in found)
        {
            var name = AttributedTypeScanner.Describe(type);
            var targetKey = CheckTargetDeviceType(name, attribute, errors);
            CheckSettingsType(name, attribute, errors);

            descriptors.Add(new RuleActionDescriptor(
                attribute.Key,
                attribute.Kind,
                string.IsNullOrWhiteSpace(attribute.DisplayName) ? attribute.Key : attribute.DisplayName,
                attribute.Description,
                attribute.TargetDeviceType,
                targetKey,
                attribute.SettingsType));
        }

        AttributedTypeScanner.ThrowIfAny(errors, "Rule action registration failed:");

        foreach (var (type, attribute) in found)
            services.AddKeyedScoped(typeof(IRuleActionHandler), attribute.Key, type);

        services.AddSingleton<IRuleActionCatalog>(new RuleActionCatalog(descriptors));
    }

    /// <summary>
    /// Checks <see cref="RuleActionAttribute.TargetDeviceType"/> against the action's kind and returns the
    /// device type key it resolves to, or <see langword="null"/> when there is none or it is invalid.
    /// </summary>
    private static string? CheckTargetDeviceType(string name, RuleActionAttribute attribute, List<string> errors)
    {
        var target = attribute.TargetDeviceType;

        if (attribute.Kind == RuleActionKind.Service)
        {
            if (target is not null)
                errors.Add($"{name} is a {nameof(RuleActionKind.Service)} action but sets {nameof(RuleActionAttribute.TargetDeviceType)}, which is only valid on {nameof(RuleActionKind.Device)} actions.");
            return null;
        }

        if (target is null)
        {
            errors.Add($"{name} is a {nameof(RuleActionKind.Device)} action but does not set {nameof(RuleActionAttribute.TargetDeviceType)}, which {nameof(RuleActionKind.Device)} actions require.");
            return null;
        }

        var deviceType = target.GetCustomAttribute<DeviceTypeAttribute>(inherit: false);
        var valid = true;

        if (deviceType is null)
        {
            errors.Add($"{name} sets {nameof(RuleActionAttribute.TargetDeviceType)} to {target.FullName}, which does not have [DeviceType].");
            valid = false;
        }
        else if (string.IsNullOrWhiteSpace(deviceType.Key))
        {
            errors.Add($"{name} sets {nameof(RuleActionAttribute.TargetDeviceType)} to {target.FullName}, whose [DeviceType] key is blank.");
            valid = false;
        }

        if (!typeof(IDeviceTypeDefinition).IsAssignableFrom(target))
        {
            errors.Add($"{name} sets {nameof(RuleActionAttribute.TargetDeviceType)} to {target.FullName}, which does not implement {nameof(IDeviceTypeDefinition)}.");
            valid = false;
        }

        return valid ? deviceType!.Key : null;
    }

    /// <summary>
    /// Checks that <see cref="RuleActionAttribute.SettingsType"/>, when set, is a non-abstract class or
    /// record that settings can be deserialized into.
    /// </summary>
    /// <remarks>
    /// Deserializes <c>{}</c> into the type with <see cref="RuleActionSettings.SettingsOptions"/>. A
    /// <see cref="JsonException"/> only means required properties are missing, which is fine. Any other
    /// exception means the type itself is unusable, for example <see cref="NotSupportedException"/> for a
    /// type with two constructors and no <c>[JsonConstructor]</c>, or <see cref="InvalidOperationException"/>
    /// for a constructor parameter that matches no property. <c>GetTypeInfo</c> is not enough: it succeeds
    /// for these types, and only the first real deserialize fails.
    /// </remarks>
    private static void CheckSettingsType(string name, RuleActionAttribute attribute, List<string> errors)
    {
        var settings = attribute.SettingsType;
        if (settings is null) return;

        if (!settings.IsClass || settings.IsAbstract || settings.ContainsGenericParameters)
        {
            errors.Add($"{name} sets {nameof(RuleActionAttribute.SettingsType)} to {settings.FullName}, which is not a non-abstract, closed class or record.");
            return;
        }

        try
        {
            JsonSerializer.Deserialize("{}", settings, RuleActionSettings.SettingsOptions);
        }
        catch (JsonException)
        {
            // Required properties are missing from {}; the type itself is usable.
        }
        catch (Exception ex)
        {
            errors.Add($"Action \"{attribute.Key}\" ({name}) sets {nameof(RuleActionAttribute.SettingsType)} to {settings.FullName}, which cannot be read as settings: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
