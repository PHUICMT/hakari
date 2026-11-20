namespace Hakari.Core.SessionNotes;

/// <summary>A hook's command line and the shell it is written for.</summary>
public sealed record HookCommand(string Command, HookShell Shell);
