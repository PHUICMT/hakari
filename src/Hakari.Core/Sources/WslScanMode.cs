namespace Hakari.Core.Sources;

public enum WslScanMode
{
    /// <summary>Never touch WSL.</summary>
    Off,

    /// <summary>Only read running distributions, so no VM is ever started.</summary>
    RunningOnly,

    /// <summary>Read every distribution. Accessing a stopped one boots its VM.</summary>
    All,
}
