using Content.Shared._PV.Ranching;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._PV.Ranching;

[UsedImplicitly]
public sealed class AnimalAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AnimalAnalyzerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AnimalAnalyzerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window != null && message is AnimalAnalyzerScannedMessage scanned)
            _window.Populate(scanned.State);
    }
}
