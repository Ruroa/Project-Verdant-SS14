namespace Content.Shared._FarHorizons.LimbDamage.Components;

/// <summary>
/// Locational damage
/// </summary>
[RegisterComponent]
public sealed partial class LimbDamageComponent : Component
{
    /// <summary>
    /// Part damage fraction
    /// </summary>
    [DataField]
    public float PartDamageFraction = 0.5f;
}
