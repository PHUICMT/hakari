using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// One row of a settings card: icon, title, optional description, and the control on the
/// right (the content).
/// </summary>
public sealed partial class SettingRow : ContentControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(SettingRow),
        new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty DescriptionVisibilityProperty =
        DependencyProperty.Register(
            nameof(DescriptionVisibility),
            typeof(Visibility),
            typeof(SettingRow),
            new PropertyMetadata(Visibility.Collapsed));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Collapsed while there is no description, so short rows stay centered.</summary>
    public Visibility DescriptionVisibility
    {
        get => (Visibility)GetValue(DescriptionVisibilityProperty);
        private set => SetValue(DescriptionVisibilityProperty, value);
    }

    /// <summary>
    /// The control on the right is named by the row's title, so a screen reader says what a
    /// switch or a choice is for, unless the control already has a name of its own.
    /// </summary>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (newContent is DependencyObject control
            && string.IsNullOrEmpty(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(
                control)))
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, Title);
        }
    }

    private static void OnDescriptionChanged(
        DependencyObject owner,
        DependencyPropertyChangedEventArgs change)
    {
        var row = (SettingRow)owner;
        row.DescriptionVisibility = string.IsNullOrEmpty(change.NewValue as string)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
