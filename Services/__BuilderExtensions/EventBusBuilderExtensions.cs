using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;
using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

/// <summary>
/// The event bus service group: everything that turns system events into rule actions and runs them.
/// </summary>
/// <remarks>
/// Depends on the database group (<see cref="DatabaseBuilderExtensions.AddOosDatabaseServices"/>):
/// <see cref="IEventRuleValidationService"/> reads devices through <see cref="Data.DataModels.V2.OosDbContext"/>.
/// It does not depend on the device group and can be added before or after it.
/// </remarks>
public static class EventBusBuilderExtensions
{
    /// <summary>
    /// Registers the event bus service group. Safe to call more than once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Today this registers the rule action handlers and the <see cref="IRuleActionCatalog"/> that
    /// describes them, found by scanning every loaded assembly (see <see cref="AddRuleActionHandlers"/>),
    /// and the scoped <see cref="IEventRuleValidationService"/>. The rest of the group joins it here as it
    /// is built.
    /// </para>
    /// <para>
    /// Only assemblies already loaded into the app domain are scanned, so load plugin assemblies before
    /// calling this. Handlers resolve their own dependencies (device actions need the device job queue),
    /// so a boot mode using this group must also register those, and the database group for rule
    /// validation.
    /// </para>
    /// </remarks>
    /// <param name="bob">The web application builder.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if rule action registration fails. See <see cref="AddRuleActionHandlers"/>.
    /// </exception>
    public static WebApplicationBuilder AddOosEventBusServices(this WebApplicationBuilder bob)
    {
        ArgumentNullException.ThrowIfNull(bob);

        AddRuleActionHandlers(bob.Services);
        bob.Services.TryAddScoped<IEventRuleValidationService, DbEventRuleValidationService>();

        return bob;
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
    /// <see cref="RuleActionAttribute.SettingsType"/> is not a non-abstract class or record. Nothing is
    /// registered when this is thrown.
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
    private static void CheckSettingsType(string name, RuleActionAttribute attribute, List<string> errors)
    {
        var settings = attribute.SettingsType;
        if (settings is null) return;

        if (!settings.IsClass || settings.IsAbstract || settings.ContainsGenericParameters)
            errors.Add($"{name} sets {nameof(RuleActionAttribute.SettingsType)} to {settings.FullName}, which is not a non-abstract, closed class or record.");
    }
}
