using System.Reflection;
using System.Text;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

/// <summary>
/// The assembly scan shared by the startup scans that discover types by attribute, such as device types
/// and rule actions.
/// </summary>
/// <remarks>
/// Callers collect every problem into one list, add their own checks to the same list, and throw once
/// with <see cref="ThrowIfAny"/>, so startup reports everything wrong at the same time.
/// </remarks>
internal static class AttributedTypeScanner
{
    /// <summary>
    /// Finds every non-abstract class carrying <typeparamref name="TAttribute"/> in the loaded assemblies
    /// that can contain one, and runs the checks every such scan needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A class with the attribute has to implement <paramref name="requiredInterface"/>, so only the
    /// assembly declaring it and the loaded assemblies that reference that assembly are scanned.
    /// Framework and other third-party assemblies are skipped, which keeps startup fast. Only assemblies
    /// already loaded into the app domain are seen.
    /// </para>
    /// <para>
    /// Adds an error to <paramref name="errors"/> when an assembly's types cannot be loaded, a class with
    /// the attribute does not implement <paramref name="requiredInterface"/> or is an open generic type, a key
    /// is blank, or two classes share a key. Every class with the attribute is still returned, so the
    /// caller can run its own checks on all of them.
    /// </para>
    /// </remarks>
    /// <typeparam name="TAttribute">The attribute that marks a class for discovery.</typeparam>
    /// <param name="requiredInterface">
    /// The interface every marked class must implement. A <see cref="Type"/> rather than a type parameter
    /// because interfaces with static abstract members, such as
    /// <see cref="Devices.Types.Interfaces.IDeviceTypeDefinition"/>, cannot be type arguments.
    /// </param>
    /// <param name="getKey">Reads the unique key from an attribute.</param>
    /// <param name="keyName">What the key identifies, for error messages, such as <c>Device type</c>.</param>
    /// <param name="errors">The list problems are added to.</param>
    /// <returns>Every marked class with its attribute, in assembly then type order.</returns>
    internal static List<(Type Type, TAttribute Attribute)> FindAttributed<TAttribute>(
        Type requiredInterface, Func<TAttribute, string?> getKey, string keyName, List<string> errors)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(requiredInterface);
        var attributeName = AttributeName<TAttribute>();
        var found = new List<(Type Type, TAttribute Attribute)>();

        foreach (var assembly in CandidateAssemblies(requiredInterface.Assembly))
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var reasons = ex.LoaderExceptions.OfType<Exception>().Select(e => e.Message).Distinct();
                errors.Add($"Could not load the types in assembly {assembly.GetName().Name}: {string.Join("; ", reasons)}");
                continue;
            }

            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract) continue;
                if (type.GetCustomAttribute<TAttribute>(inherit: false) is not { } attribute) continue;

                var name = Describe(type);
                var key = getKey(attribute);

                if (!requiredInterface.IsAssignableFrom(type))
                    errors.Add($"{name} has [{attributeName}(\"{key}\")] but does not implement {requiredInterface.Name}.");
                if (type.IsGenericTypeDefinition)
                    errors.Add($"{name} has [{attributeName}] but is an open generic type, which cannot be used.");
                if (string.IsNullOrWhiteSpace(key))
                    errors.Add($"{name} has [{attributeName}] with a null, empty or whitespace key.");

                found.Add((type, attribute));
            }
        }

        foreach (var group in found
                     .Select(f => (f.Type, Key: getKey(f.Attribute)))
                     .Where(f => !string.IsNullOrWhiteSpace(f.Key))
                     .GroupBy(f => f.Key!, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            errors.Add($"{keyName} key \"{group.Key}\" is declared by more than one type: {string.Join(", ", group.Select(f => Describe(f.Type)))}.");
        }

        return found;
    }

    /// <summary>
    /// Throws one exception listing every problem, if there are any.
    /// </summary>
    /// <param name="errors">The problems collected by the scan.</param>
    /// <param name="heading">The first line of the message, such as <c>Device type registration failed:</c>.</param>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="errors"/> is not empty.</exception>
    internal static void ThrowIfAny(List<string> errors, string heading)
    {
        if (errors.Count == 0) return;

        var message = new StringBuilder().AppendLine(heading);
        foreach (var error in errors) message.Append("  - ").AppendLine(error);
        throw new InvalidOperationException(message.ToString().TrimEnd());
    }

    /// <summary>A type's full name and its assembly, for error messages.</summary>
    internal static string Describe(Type type) => $"{type.FullName} ({type.Assembly.GetName().Name})";

    /// <summary>
    /// The loaded assemblies that can declare implementations of a type in <paramref name="core"/>: that
    /// assembly, and any that reference it.
    /// </summary>
    internal static IEnumerable<Assembly> CandidateAssemblies(Assembly core)
    {
        var coreName = core.GetName().Name;

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a => a == core || a.GetReferencedAssemblies().Any(r => r.Name == coreName))
            .OrderBy(a => a.FullName, StringComparer.Ordinal);
    }

    /// <summary>The attribute's name as written in code, such as <c>DeviceType</c>.</summary>
    private static string AttributeName<TAttribute>() where TAttribute : Attribute
    {
        var name = typeof(TAttribute).Name;
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;
    }
}
