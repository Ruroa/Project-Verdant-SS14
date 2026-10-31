using Content.Server.Administration;
using Content.Server.Parallax;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.Maps;

[AdminCommand(AdminFlags.Mapping)]
public sealed partial class PVPlanetCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private PlanetarySurfaceSetupSystem _setup = default!;

    public override string Command => "pvplanet";
    public override string Description => Loc.GetString("cmd-pvplanet-desc");
    public override string Help => Loc.GetString("cmd-pvplanet-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var mapInt))
        {
            shell.WriteError(Loc.GetString("cmd-pvplanet-args"));
            return;
        }

        var mapId = new MapId(mapInt);
        if (!_map.MapExists(mapId))
        {
            shell.WriteError(Loc.GetString("cmd-pvplanet-map", ("map", mapId)));
            return;
        }

        _setup.Configure(_map.GetMapOrInvalid(mapId));
        shell.WriteLine(Loc.GetString("cmd-pvplanet-success", ("map", mapId)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.MapIds(_entities), "Map Id")
            : CompletionResult.Empty;
    }
}
