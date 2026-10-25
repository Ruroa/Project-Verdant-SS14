namespace Content.Shared._PV.Ranching;

/// <summary>
/// Feed trough visuals
/// </summary>
[RegisterComponent]
public sealed partial class FeedingTroughComponent : Component
{
    public const string SolutionName = "food";
}

/// <summary>
/// Water trough
/// </summary>
[RegisterComponent]
public sealed partial class WaterTroughComponent : Component
{
    public const string SolutionName = "trough";
}
