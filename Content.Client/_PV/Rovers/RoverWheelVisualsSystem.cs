using Content.Shared._PV.Rovers;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._PV.Rovers;

public sealed class RoverWheelVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoverWheelComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, RoverWheelComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !args.AppearanceData.TryGetValue(RoverWheelVisuals.Rotation, out var rotation)
            || rotation is not float radians)
            return;

        _sprite.SetRotation((uid, args.Sprite), new Angle(radians));
    }
}
