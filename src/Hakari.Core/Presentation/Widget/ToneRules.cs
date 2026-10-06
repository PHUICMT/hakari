using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <summary>Which color a limit is drawn in, by how full it is.</summary>
public readonly record struct ToneRules(
    int WarnAt = WidgetLayout.DefaultWarnAt,
    int CriticalAt = WidgetLayout.DefaultCriticalAt)
{
    public static ToneRules Default => new();

    public static ToneRules Of(WidgetLayout layout) => new(layout.WarnAt, layout.CriticalAt);

    /// <summary>Old readings stay muted until they pass a threshold.</summary>
    public LineTone Of(double percent, LimitFreshness freshness)
    {
        if (percent >= CriticalAt)
        {
            return LineTone.Critical;
        }

        if (percent >= WarnAt)
        {
            return LineTone.Warning;
        }

        return freshness == LimitFreshness.LastKnown ? LineTone.Muted : LineTone.Normal;
    }
}
