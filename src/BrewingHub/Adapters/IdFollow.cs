using System;
using System.Collections.Generic;
using ExiledToolkit.Game;
using GameCore.HotUpdate.Battle.Logic;
using MapSearcher = Il2CppSystem.Collections.Generic.Dictionary<long, long>;

namespace BrewingHub.Adapters;

/// <summary>Hands the session each map from old barrel ids to new ones, which the game builds on every load and every trip.</summary>
internal static class IdFollow
{
    private const string Following = "following barrels to their new ids";

    private static PatchKit? _hooks;
    private static BrewSession? _session;

    public static void Apply(PatchKit hooks, BrewSession session)
    {
        _hooks = hooks;
        _session = session;

        // AgentManager.StartBattle hands its map to this call after it makes every agent again.
        hooks.Patch(typeof(IdFollow), typeof(PowerManager), nameof(PowerManager.RemapInstanceIds), new[] { typeof(MapSearcher) },
            prefix: nameof(BeforeRemap), done: "hooked PowerManager.RemapInstanceIds",
            consequence: "a barrel set to brew again stops doing so after a load or a trip");
    }

    private static void BeforeRemap(MapSearcher __0)
    {
        try
        {
            if (_session == null || __0 == null) return;

            var map = new Dictionary<long, long>();

            foreach (var pair in __0) map[pair.Key] = pair.Value;

            _session.Follow(map);
        }
        catch (Exception ex)
        {
            _hooks?.Fail(Following, ex);
        }
    }
}
