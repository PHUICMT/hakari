using Hakari.Core.Limits;
using Hakari.Core.Localization;

namespace Hakari.Core.Presentation;

/// <summary>A budget passed or a costly session, in the same shape as a limit alert.</summary>
public static class SpendAlertText
{
    private const double FullBar = 1;

    public static LimitAlertMessage Compose(SpendAlert alert, string currency)
    {
        var spent = MoneyText.Format(alert.Spent, currency);
        var mark = MoneyText.Format(alert.Mark, currency);
        return alert.Kind switch
        {
            SpendAlertKind.DailyBudget => Budget("alert.budgetDay", spent, mark),
            SpendAlertKind.MonthlyBudget => Budget("alert.budgetMonth", spent, mark),
            _ => new LimitAlertMessage(
                Texts.Get("alert.unusual"),
                Texts.Format("alert.unusual.body", spent),
                Texts.Format("alert.unusual.detail", mark),
                FullBar,
                Texts.Get("alert.unusual.label"),
                Texts.Format("alert.times", decimal.Round(alert.Spent / alert.Mark, 0)),
                IsWarning: true),
        };
    }

    private static LimitAlertMessage Budget(string titleKey, string spent, string budget) => new(
        Texts.Format(titleKey, budget),
        Texts.Format("alert.budget.body", spent, budget),
        string.Empty,
        FullBar,
        Texts.Get("alert.budget.label"),
        spent,
        IsWarning: true);
}
