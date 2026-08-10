namespace Content.Shared._PV.Ranching;

/// <summary>
/// Gives a six-slot feeding trough its special empty/half/full visual thresholds.
/// </summary>
[RegisterComponent]
public sealed partial class FeedingTroughComponent : Component
{
    [DataField]
    public float UseRange = 1.5f;

    [DataField]
    public TimeSpan UseCooldown = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextUse;
}

/// <summary>
/// Allows thirsty nearby animals to drink water from a solution trough.
/// </summary>
[RegisterComponent]
public sealed partial class WaterTroughComponent : Component
{
    public const string SolutionName = "trough";

    [DataField]
    public float UseRange = 1.5f;

    [DataField]
    public int DrinkAmount = 5;

    [DataField]
    public float ThirstPerUnit = 4f;

    [DataField]
    public TimeSpan UseCooldown = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextUse;
}
