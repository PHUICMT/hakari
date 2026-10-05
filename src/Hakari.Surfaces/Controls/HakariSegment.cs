using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// One choice inside a <see cref="HakariSegmented"/>. Its own template draws the text and
/// hover fill; the selection indicator belongs to the segmented control, so it can slide.
/// </summary>
public sealed partial class HakariSegment : RadioButton;
