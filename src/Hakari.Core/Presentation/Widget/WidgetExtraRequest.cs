namespace Hakari.Core.Presentation.Widget;

/// <summary>Which extra format values to read, and what some of them need from settings.</summary>
/// <param name="Names">Names the formats use, from <see cref="WidgetFormat.NamesIn"/>.</param>
/// <param name="DailyBudget">The day's budget in the shown currency, or null for none.</param>
/// <param name="PlanPriceOf">An account's plan price a month in the shown currency.</param>
public sealed record WidgetExtraRequest(
    IReadOnlyCollection<string> Names,
    decimal? DailyBudget = null,
    Func<WidgetAccount, decimal?>? PlanPriceOf = null);
