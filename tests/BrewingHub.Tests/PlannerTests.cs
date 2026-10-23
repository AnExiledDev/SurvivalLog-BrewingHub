using BrewingHub.Core;
using Xunit;
using static BrewingHub.Tests.FakeGame;

namespace BrewingHub.Tests;

/// <summary>Filling a barrel for one drink from the food in reach, and the one-step changes that make a better drink.</summary>
public class PlannerTests
{
    private static readonly Rules Shipped = Rules.Shipped;

    [Fact]
    public void A_strong_barrel_fills_to_the_last_bottle_and_stops()
    {
        var plan = Planner.For(Shipped, new[] { S(1, Berry, 30) }, 2, true, true);

        Assert.NotNull(plan);
        Assert.Equal(23, plan!.Units);
        Assert.Equal(9, plan.Brew.Bottles);
        Assert.Equal(2, plan.Brew.Tier);
    }

    [Fact]
    public void Spoiled_food_is_never_used()
    {
        var plan = Planner.For(Shipped, new[] { S(1, Berry, 30, hours: 0), S(2, Berry, 5, hours: 10) }, 2, true, true);

        Assert.NotNull(plan);
        Assert.Equal(new long[] { 2 }, plan!.Takes.Select(take => take.Stack.InstanceId));
        Assert.Equal(5, plan.Units);
    }

    [Fact]
    public void Food_closest_to_spoiling_goes_first_or_last_as_the_player_chose()
    {
        var pool = new[] { S(1, Berry, 30, hours: 100), S(2, Berry, 30, hours: 5) };

        Assert.Equal(new long[] { 2 }, Planner.For(Shipped, pool, 2, true, true)!.Takes.Select(take => take.Stack.InstanceId));
        Assert.Equal(new long[] { 1 }, Planner.For(Shipped, pool, 2, false, true)!.Takes.Select(take => take.Stack.InstanceId));
    }

    [Fact]
    public void Too_little_food_for_a_brew_gives_no_plan()
    {
        Assert.Null(Planner.For(Shipped, new[] { S(1, Berry, 2) }, 2, true, true));
    }

    [Fact]
    public void Without_splitting_whole_stacks_go_in_and_a_stack_that_overfills_stays_out()
    {
        var plan = Planner.For(Shipped, new[] { S(1, Berry, 10, hours: 1), S(2, Berry, 10, hours: 2), S(3, Berry, 10, hours: 3) }, 2, true, false);

        Assert.NotNull(plan);
        Assert.Equal(new[] { 10, 10 }, plan!.Takes.Select(take => take.Units));
        Assert.Equal(8, plan.Brew.Bottles);
    }

    [Fact]
    public void An_average_barrel_keeps_strong_food_from_pushing_it_over_the_line()
    {
        var pool = new[] { S(1, Berry, 30, hours: 1), S(2, Mushroom, 30, hours: 50) };
        var plan = Planner.For(Shipped, pool, 1, true, true);

        Assert.NotNull(plan);
        Assert.Equal(1, plan!.Brew.Tier);
        Assert.True(plan.Brew.Bottles >= 8, plan.Brew.Bottles.ToString());
    }

    [Fact]
    public void Food_outside_the_band_that_spoils_first_does_not_shut_out_the_drink()
    {
        var pool = new[] { S(1, Mushroom, 20, hours: 5), S(2, Berry, 30, hours: 50) };
        var plan = Planner.For(Shipped, pool, 2, true, true);

        Assert.NotNull(plan);
        Assert.Equal(2, plan!.Brew.Tier);
        Assert.Equal(9, plan.Brew.Bottles);
    }

    [Fact]
    public void Two_foods_outside_the_band_can_still_mix_into_it()
    {
        var plan = Planner.For(Shipped, new[] { S(1, Berry, 30), S(2, Potato, 30) }, 1, true, true);

        Assert.NotNull(plan);
        Assert.Equal(1, plan!.Brew.Tier);
    }

    [Fact]
    public void Nudges_put_a_better_drink_before_one_more_bottle()
    {
        var contents = new[] { S(1, Potato, 2, owner: BarrelA) };
        var mix = new Mix(40, -8, 2);
        var nudges = Nudges.For(Shipped, mix, contents, new[] { S(2, Berry, 3), S(3, Stew, 1, owner: Chest) }, true, true);

        Assert.Equal(new[] { (NudgeKind.Add, Stew, 2), (NudgeKind.Add, Berry, 2) }, nudges.Select(n => (n.Kind, n.Stack.ItemId, n.Next.Bottles)));
    }

    [Fact]
    public void Taking_out_the_food_that_drags_the_mix_down_is_a_nudge_when_the_rest_can_still_start()
    {
        var contents = new[] { S(1, Potato, 1, owner: BarrelA), S(2, Berry, 3, owner: BarrelA) };
        var nudges = Nudges.For(Shipped, new Mix(50, 14, 4), contents, Array.Empty<Stack>(), true, true);

        Assert.Equal((NudgeKind.Remove, Potato, 2), nudges.Select(n => (n.Kind, n.Stack.ItemId, n.Next.Tier)).Single());
    }

    [Fact]
    public void An_empty_barrel_and_spoiled_food_give_no_nudges()
    {
        Assert.Empty(Nudges.For(Shipped, Mix.None, Array.Empty<Stack>(), new[] { S(1, Stew, 1) }, true, true));
        Assert.Empty(Nudges.For(Shipped, new Mix(40, -8, 2), new[] { S(1, Potato, 2, owner: BarrelA) }, new[] { S(2, Stew, 1, hours: 0) }, true, true));
    }
}
