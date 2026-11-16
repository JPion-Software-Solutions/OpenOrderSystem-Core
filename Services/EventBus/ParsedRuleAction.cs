using System.Text.Json;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// One action entry from an event rule body that parsed cleanly.
/// </summary>
/// <param name="Index">
/// The entry's position in the body's <c>actions</c> array, so later checks can report errors at
/// <c>actions[n]</c>.
/// </param>
/// <param name="Action">The registered action the entry names.</param>
/// <param name="Target">
/// The target device's key. Always set for device actions and always <see langword="null"/> for service
/// actions. Only its shape has been checked; whether the device exists is checked by
/// <see cref="Interfaces.IEventRuleValidationService"/>.
/// </param>
/// <param name="Settings">
/// The entry's settings as a JSON object: an empty object when the entry omits them. Cloned, so it
/// outlives the parsed document.
/// </param>
public sealed record ParsedRuleAction(int Index, RuleActionDescriptor Action, string? Target, JsonElement Settings);
