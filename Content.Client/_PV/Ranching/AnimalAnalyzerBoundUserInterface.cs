using Content.Shared._PV.Ranching;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._PV.Ranching;

[UsedImplicitly]
public sealed class AnimalAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AnimalAnalyzerWindow? _window;
    private AnimalAnalyzerUiState? _lastScan;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AnimalAnalyzerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        // The scan message can arrive before the window has finished opening.
        if (_lastScan is { } scan)
            _window.Populate(scan);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is not AnimalAnalyzerScannedMessage scanned)
            return;

        _lastScan = scanned.State;
        _window?.Populate(scanned.State);
    }
}
