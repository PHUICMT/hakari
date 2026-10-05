namespace Hakari.Taskbar.Rendering;

/// <summary>What each line shows in a frame: its text, and the text it replaces if any.</summary>
internal sealed record WidgetLines(LineChange Primary, LineChange Secondary)
{
    public static WidgetLines From(WidgetFrame frame)
    {
        var previous = frame.IsChanging ? frame.Previous : null;
        return new WidgetLines(
            LineChange.Between(previous?.PrimaryText, frame.Current.PrimaryText),
            LineChange.Between(previous?.SecondaryText, frame.Current.SecondaryText));
    }
}
