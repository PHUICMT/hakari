using Hakari.Core.Localization;
using Microsoft.UI.Xaml.Markup;

namespace Hakari.Surfaces.Localization;

/// <summary>
/// XAML access to the language files: <c>Text="{l:Text Key=settings.sources}"</c>. Read when
/// the window is built; a language change rebuilds the window.
/// </summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed partial class Text : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue() => Texts.Get(Key);
}
