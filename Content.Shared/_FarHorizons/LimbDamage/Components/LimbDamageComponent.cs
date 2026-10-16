namespace Content.Shared._FarHorizons.LimbDamage.Components;

/// <summary>
/// Enables locational damage for a body. A portion of each incoming hit is recorded
/// on one of the body's attached parts, while the remainder still affects the body
/// entity and its normal mob-state thresholds.
/// </summary>
[RegisterComponent]
public sealed partial class LimbDamageComponent : Component
{
    /// <summary>
    /// Fraction of a hit recorded on a random body part. The rest remains on
    /// the main body entity so existing health thresholds continue to work.
    /// </summary>
    [DataField]
    public float PartDamageFraction = 0.5f;
}
