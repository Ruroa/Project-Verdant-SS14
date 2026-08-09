using Robust.Shared.Audio;

namespace Content.Server._PV.Ranching;

[RegisterComponent]
public sealed partial class AnimalAnalyzerComponent : Component
{
    [DataField] public TimeSpan ScanDelay = TimeSpan.FromSeconds(1);
    [DataField] public SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");
}
