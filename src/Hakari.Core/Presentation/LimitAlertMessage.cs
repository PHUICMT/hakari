namespace Hakari.Core.Presentation;

/// <summary>
/// A notification about a limit: a title, a line saying whose and when it resets, a line
/// with what comes next (when it fills at this pace, or which account still has room), and
/// the limit as a progress bar.
/// </summary>
/// <param name="Progress">0 to 1, for the bar.</param>
/// <param name="ProgressLabel">What the bar is, such as "Weekly limit".</param>
/// <param name="ProgressValue">The bar's figure, such as "87%".</param>
public sealed record LimitAlertMessage(
    string Title,
    string Body,
    string Detail,
    double Progress,
    string ProgressLabel,
    string ProgressValue,
    bool IsWarning);
