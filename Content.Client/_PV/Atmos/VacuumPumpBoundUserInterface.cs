using Content.Shared._PV.Atmos;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._PV.Atmos;

[UsedImplicitly]
public sealed class VacuumPumpBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private VacuumPumpWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<VacuumPumpWindow>();
        _window.OnTogglePressed += () => SendMessage(new VacuumPumpToggleMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is VacuumPumpUiState pumpState)
            _window?.UpdateState(pumpState);
    }
}
