using Content.Shared.DoAfter;
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
