namespace Hakari.Core.Parsing;

/// <summary>The short title Claude Code gives a session, or the user's own name for it.</summary>
public sealed record SessionTitle(string SessionId, string Title, bool IsCustom);
