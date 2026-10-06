using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>How long ago something happened, in the largest whole unit: "14 min ago".</summary>
public static class AgeText
{
    public static string Format(TimeSpan age) => age switch
    {
        _ when age < TimeSpan.FromMinutes(1) => Texts.Get("age.justNow"),
        _ when age < TimeSpan.FromHours(1) => Texts.Format("age.minutes", (int)age.TotalMinutes),
        _ when age < TimeSpan.FromDays(1) => Texts.Format("age.hours", (int)age.TotalHours),
        _ => Texts.Format("age.days", (int)age.TotalDays),
    };
}
