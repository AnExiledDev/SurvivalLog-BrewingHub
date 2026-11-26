using System;
using System.Collections.Generic;
using System.Linq;

namespace BrewingHub.Core;

/// <summary>
/// A barrel's id changes on every load and every trip. The game makes each barrel again and hands out a map from the old
/// id to the new one. A trip's map leaves out the barrels at home, and they come back in the map on the way home, so an
/// id the map leaves out is kept as it is. The game never hands out the same id twice while it runs.
/// </summary>
public static class IdMoves
{
    public static void Follow(ISet<long> ids, IReadOnlyDictionary<long, long> map)
    {
        var moved = ids.Where(map.ContainsKey).ToList();

        foreach (var id in moved) ids.Remove(id);
        foreach (var id in moved) ids.Add(map[id]);
    }

    /// <summary>Moves each value to its barrel's new id; renamed gets the value and the new id, for a value that holds the id too.</summary>
    public static void Follow<T>(IDictionary<long, T> byId, IReadOnlyDictionary<long, long> map, Func<T, long, T> renamed)
    {
        var moved = byId.Where(pair => map.ContainsKey(pair.Key)).ToList();

        foreach (var pair in moved) byId.Remove(pair.Key);
        foreach (var pair in moved) byId[map[pair.Key]] = renamed(pair.Value, map[pair.Key]);
    }
}
