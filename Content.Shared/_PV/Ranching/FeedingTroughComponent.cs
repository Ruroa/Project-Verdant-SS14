namespace Content.Shared._PV.Ranching;

/// <summary>
/// Gives a six-slot feeding trough its special empty/half/full visual thresholds.
/// </summary>
[RegisterComponent]
public sealed partial class FeedingTroughComponent : Component
{
    public const string SolutionName = "food";
}

/// <summary>
/// Allows thirsty nearby animals to drink water from a solution trough.
/// </summary>
[RegisterComponent]
public sealed partial class WaterTroughComponent : Component
{
    public const string SolutionName = "trough";
}
