using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._PV.Ranching;

[Serializable, NetSerializable]
public enum AnimalAnalyzerUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed partial class AnimalAnalyzerDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class AnimalAnalyzerScannedMessage(AnimalAnalyzerUiState state) : BoundUserInterfaceMessage
{
    public AnimalAnalyzerUiState State = state;
}

[Serializable, NetSerializable]
public readonly record struct AnimalAnalyzerUiState(string Name, float FoodPercent, float WaterPercent,
    string LifeStage, string BreedingStatus);

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class AnimalFeedComponent : Component
{
    /// <summary>
    /// Wheat-gold is the default for admin-spawned feed. The feed maker replaces it with the produce color.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#D2B15F");
}

[Serializable, NetSerializable]
public enum AnimalFeedVisualLayers : byte
{
    Feed
}
