namespace OpenOrderSystem.Core.Ui;

/// <summary>
/// Marks a UI controller as the home controller: in every boot mode it is active in, its <c>Index</c>
/// action is also served at <c>/</c>. It keeps its folder URL as well (<c>Ui/Oos/</c> → <c>/oos</c>).
/// At most one home controller may be active per boot mode.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OosUiHomeAttribute : Attribute;
