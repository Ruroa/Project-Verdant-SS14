using System.Linq;
using Content.Shared._NullLink;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager : INullLinkPlayerManager
{
    private void UpdateTitleBuilder(string obj)
    {
        if (_builder?.ID == obj)
            return;
        if (!_proto.TryIndex<TitleBuilderPrototype>(obj, out var builder))
            return;
        _builder = builder;

        foreach (var player in _playerById)
            RebuildTitle(player.Key, player.Value);
    }

    private void RebuildTitle(Guid player, PlayerData playerData)
    {
        var adminData = _adminManager.GetAdminData(playerData.Session);
        playerData.Title = adminData?.Title is { Length: > 0 } adminTitle
            ? $"-{adminTitle}-"
            : null;
    }
}
