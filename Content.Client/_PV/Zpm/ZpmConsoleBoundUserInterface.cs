using Content.Shared._PV.Zpm;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._PV.Zpm;

[UsedImplicitly]
public sealed class ZpmConsoleBoundUserInterface : BoundUserInterface
{
    private ZpmConsoleWindow? _window;

    public ZpmConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ZpmConsoleWindow>();
        _window.RefreshPressed += () => SendMessage(new ZpmConsoleRefreshMessage());
        _window.StartPressed += () => SendMessage(new ZpmStartMessage());
        _window.EjectCorePressed += () => SendMessage(new ZpmEjectCoreMessage());
        _window.PowerLevelSelected += level => SendMessage(new ZpmSetPowerLevelMessage(level));
        _window.ShutdownPressed += emergency => SendMessage(new ZpmShutdownMessage(emergency));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ZpmConsoleBoundInterfaceState zpmState)
            _window?.UpdateState(zpmState);
    }
}
