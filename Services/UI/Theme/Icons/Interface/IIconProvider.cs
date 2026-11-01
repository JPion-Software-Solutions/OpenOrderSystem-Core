// OpenOrderSystem.Core/Services/UI/Theme/Icons/Interface/IIconProvider.cs

using Microsoft.AspNetCore.Html;

namespace OpenOrderSystem.Core.Services.UI.Theme.Icons.Interface;

/// <summary>
/// A source of icon markup for semantic icon keys, layered like resource packs:
/// the resolver asks providers from the top of the ladder down and the first answer wins.
/// </summary>
/// <remarks>
/// A provider does not need to cover every key. Returning <c>null</c> from
/// <see cref="ResolveIcon"/> lets lower providers fill in, so a small pack can
/// override a handful of icons and inherit the rest.
/// </remarks>
public interface IIconProvider
{
    /// <summary>
    /// Stable identifier, used to store this provider's position on the priority ladder.
    /// </summary>
    /// <remarks>
    /// Must be unique among registered providers and must not change between releases;
    /// renaming a provider drops its saved position.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Human-readable summary of the icon set, shown wherever providers are listed
    /// (for example, when ordering the priority ladder).
    /// </summary>
    string Description { get; }

    /// <summary>
    /// URL of a preview image representing the icon set, shown alongside
    /// <see cref="Description"/> when providers are listed.
    /// </summary>
    string ThumbnailUrl { get; }

    /// <summary>
    /// The semantic icon keys this provider supplies markup for.
    /// </summary>
    /// <remarks>
    /// Advertises coverage only; it does not limit which keys the resolver may pass to
    /// <see cref="ResolveIcon"/>. Namespaced plugin keys (<c>pluginid:name</c>) do not
    /// need to be listed here.
    /// </remarks>
    string[] IconKeys { get; }

    /// <summary>
    /// Resolves the markup for an icon key.
    /// </summary>
    /// <param name="key">A key from <see cref="IconKeys"/>, or a namespaced plugin key (<c>pluginid:name</c>).</param>
    /// <returns>
    /// <c>null</c> to pass to the next provider; an empty string to deliberately show no icon;
    /// otherwise HTML markup for the icon.
    /// </returns>
    /// <remarks>
    /// The returned markup is rendered as-is, so it must be trusted, well-formed HTML.
    /// </remarks>
    HtmlString? ResolveIcon(string key);
}
