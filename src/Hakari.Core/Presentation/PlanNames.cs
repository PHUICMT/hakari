using Hakari.Core.Accounts;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>A plan's current name, from <see cref="PlanCatalog"/>.</summary>
public static class PlanNames
{
    public static string Short(SubscriptionPlan plan) =>
        PlanCatalog.Current.Find(plan)?.Name is { Length: > 0 } name
            ? name
            : Texts.Get("plan.unknown");
}
