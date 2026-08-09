using Robust.Shared.Prototypes;

namespace Content.Server._PV.Ranching;

[RegisterComponent]
public sealed partial class FeedMakerComponent : Component
{
    [DataField] public EntProtoId FeedPrototype = "PVAnimalFeed";
    [DataField] public int FeedAmount = 2;
}
