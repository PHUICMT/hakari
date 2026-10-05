using Hakari.Core.Sources;

namespace Hakari.Core.Limits;

public interface IClaudeCodeActivity
{
    /// <summary>
    /// True when Claude Code may be using this source's sign-in, in which case Hakari leaves
    /// refreshing to Claude Code so the two never rotate the same refresh token.
    /// </summary>
    bool IsRunning(UsageSource source);
}
