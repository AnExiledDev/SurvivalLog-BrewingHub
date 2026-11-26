using System.Collections.Generic;
using BrewingHub.Core;
using Xunit;

namespace BrewingHub.Tests;

public class IdMovesTests
{
    [Fact]
    public void A_barrel_set_to_brew_again_keeps_the_switch_under_its_new_id()
    {
        var again = new HashSet<long> { 10, 20 };

        IdMoves.Follow(again, new Dictionary<long, long> { [10] = 110, [20] = 120 });

        Assert.Equal(new HashSet<long> { 110, 120 }, again);
    }

    [Fact]
    public void Barrels_left_out_of_a_trip_map_keep_their_ids_for_the_way_home()
    {
        // A trip's map has only the trip's agents and you; the barrels at home come back in the map on the way home.
        var again = new HashSet<long> { 10 };

        IdMoves.Follow(again, new Dictionary<long, long> { [99] = 199 });
        Assert.Equal(new HashSet<long> { 10 }, again);

        IdMoves.Follow(again, new Dictionary<long, long> { [10] = 310 });
        Assert.Equal(new HashSet<long> { 310 }, again);
    }

    [Fact]
    public void A_barrel_as_last_seen_moves_and_carries_its_new_id()
    {
        var seen = new Dictionary<long, Barrel> { [10] = Barrel(10), [30] = Barrel(30) };

        IdMoves.Follow(seen, new Dictionary<long, long> { [10] = 110 }, (barrel, id) => barrel with { FurnitureId = id });

        Assert.Equal(new[] { 30L, 110L }, Sorted(seen.Keys));
        Assert.Equal(110, seen[110].FurnitureId);
        Assert.Equal(30, seen[30].FurnitureId);
    }

    [Fact]
    public void A_new_id_that_was_another_barrels_old_id_does_not_lose_either_one()
    {
        var tiers = new Dictionary<long, int> { [10] = 1, [20] = 2 };

        IdMoves.Follow(tiers, new Dictionary<long, long> { [10] = 20, [20] = 30 }, (tier, _) => tier);

        Assert.Equal(1, tiers[20]);
        Assert.Equal(2, tiers[30]);
        Assert.Equal(2, tiers.Count);
    }

    private static Barrel Barrel(long id) => new(id, "Brewing Barrel", BarrelState.Done, false, 7, 3, 0, 24, new List<Stack>());

    private static List<long> Sorted(IEnumerable<long> ids)
    {
        var list = new List<long>(ids);

        list.Sort();

        return list;
    }
}
