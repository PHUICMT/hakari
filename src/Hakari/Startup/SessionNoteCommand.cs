using Hakari.Core.Interprocess;
using Hakari.Core.SessionNotes;

namespace Hakari.Startup;

/// <summary>
/// The hook mode of Hakari.exe. It starts no window and no resident copy, answers from what
/// the running copy already saved, and never fails the session: any problem prints nothing.
/// </summary>
internal static class SessionNoteCommand
{
    public static int Run()
    {
        try
        {
            var answer = UsageNoteHook.Run(Console.In.ReadToEnd(), DateTimeOffset.UtcNow);
            if (answer.Output is { } output)
            {
                Console.Out.Write(output);
                Console.Out.Flush();
            }

            if (answer.WantsFreshLimits)
            {
                ResidentChannel.TrySend(ResidentCommand.RefreshLimitsSoon);
            }
        }
        catch (Exception exception)
        {
            // A failing hook would show an error in every session, so it exits as if fine;
            // the reason goes only to the log Claude Code keeps for hooks.
            Console.Error.WriteLine($"Hakari: {exception.GetType().Name}: {exception.Message}");
        }

        return 0;
    }
}
