namespace Content.Shared._PV.Weapons.Components;

[RegisterComponent]
public sealed partial class StaminaDamageOnShootComponent : Component
{
    [DataField]
    public float Damage = 15f;
}
