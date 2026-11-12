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

    // Real items with their satiety and morale per item (data dump, game 1.1.20719), for the barrel in the bug report.
    private const int Watermelon = 2539;
    private const int FujiApple = 2127;
    private const int Strawberry = 2533;
    private const int RealPotato = 2527;
    private const int WhiteBeech = 2513;
    private const int Garlic = 2537;
    private const string Freezer = "Large-Capacity Freezer";
    private const string Box = "Wooden Box";

    // A 30% bottle yield talent: 17.5 satiety a bottle, 157.5 for a full barrel. The reported numbers only add up with it.
    private static readonly Rules Talented = Shipped with { PerBottle = 17.5 };

    private static Stack Food(long instance, int item, int count, string place, double? hours = null) => item switch
    {
        Watermelon => new Stack(instance, item, count, Role, place, hours, 60, 30),
        FujiApple => new Stack(instance, item, count, Role, place, hours, 5, 3),
        Strawberry => new Stack(instance, item, count, Chest, place, hours, 2, 3),
        RealPotato => new Stack(instance, item, count, Chest, place, hours, 3.5, -3),
        WhiteBeech => new Stack(instance, item, count, Chest, place, hours, 2.5, -3),
        Garlic => new Stack(instance, item, count, Chest, place, hours, 8, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };

    private static Stack Made(long instance, int item, int count, double sat, double mood, double? hours = null) =>
        new(instance, item, count, Role, "Bag", hours, sat, mood);

    private static int Lines(Plan plan) => plan.Takes.Select(take => (take.Stack.ItemId, take.Stack.Place)).Distinct().Count();

    [Fact]
    public void A_few_watermelons_fill_a_strong_barrel_so_only_watermelons_are_suggested()
    {
        // The reported Strong barrel took 2 Watermelons, a Fuji Apple and 17 Strawberries from three freezer stacks.
        var pool = new[]
        {
            Food(1, Watermelon, 3, "Bag", 200),
            Food(2, FujiApple, 1, "Bag", 20),
            Food(3, Strawberry, 4, Freezer, 10),
            Food(4, Strawberry, 9, Freezer, 30),
            Food(5, Strawberry, 4, Freezer, 40),
        };
        var plan = Planner.For(Talented, pool, 2, true, true);

        Assert.NotNull(plan);
        Assert.Equal(new[] { (Watermelon, 3) }, plan!.Takes.Select(take => (take.Stack.ItemId, take.Units)));
        Assert.Equal(9, plan.Brew.Bottles);
        Assert.Equal(2, plan.Brew.Tier);
    }

    [Fact]
    public void An_average_barrel_from_the_same_food_is_two_lines_not_a_long_mix()
    {
        // The reported Average barrel took 1 Fuji Apple, 40 Strawberries, 8 Potatoes, 15 White Beech Mushrooms and a Garlic.
        var pool = new[]
        {
            Food(1, Watermelon, 3, "Bag", 200),
            Food(2, FujiApple, 1, "Bag", 20),
            Food(3, Strawberry, 4, Freezer, 10),
            Food(4, Strawberry, 9, Freezer, 30),
            Food(5, Strawberry, 20, Freezer, 40),
            Food(6, Strawberry, 7, Freezer, 50),
            Food(7, RealPotato, 8, Box, 15),
            Food(8, WhiteBeech, 15, Box, 12),
            Food(9, Garlic, 1, Box, 100),
        };
        var plan = Planner.For(Talented, pool, 1, true, true);

        Assert.NotNull(plan);
        Assert.Equal(1, plan!.Brew.Tier);
        Assert.Equal(9, plan.Brew.Bottles);
        Assert.True(Lines(plan) <= 2, string.Join(", ", plan.Takes.Select(take => $"{take.Units} x {take.Stack.ItemId} in {take.Stack.Place}")));
        Assert.True(plan.Brew.Wasted <= 1, $"{plan.Brew.Wasted} wasted, the reported barrel wasted 1");
    }

    [Fact]
    public void Of_two_foods_that_each_fill_the_barrel_in_9_items_the_one_with_the_smaller_leftover_wins()
    {
        // 9 of the first make 234 satiety, 9 over the last bottle; 9 of the second make 229.5, 4.5 over. The first
        // spoils sooner and is more filling, so filling in either of those orders would take it.
        var pool = new[] { Made(1, 901, 9, 26, 20, hours: 5), Made(2, 902, 9, 25.5, 20, hours: 100) };
        var plan = Planner.For(Shipped, pool, 2, true, true);

        Assert.NotNull(plan);
        Assert.Equal(new[] { (902, 9) }, plan!.Takes.Select(take => (take.Stack.ItemId, take.Units)));
        Assert.Equal(4.5, plan.Brew.Wasted, 6);
    }

    [Fact]
    public void A_filling_food_beats_a_long_list_of_small_food_that_spoils_sooner()
    {
        var pool = new[] { S(1, Berry, 30, hours: 5), S(2, Stew, 8, owner: Chest) };
        var plan = Planner.For(Shipped, pool, 2, true, true);

        Assert.NotNull(plan);
        Assert.Equal(new[] { (Stew, 8) }, plan!.Takes.Select(take => (take.Stack.ItemId, take.Units)));
        Assert.Equal(9, plan.Brew.Bottles);
    }

    [Fact]
    public void Food_that_adds_no_bottle_stays_out()
    {
        // 3 Berries make the one bottle 4 would; the fourth would only be lost.
        var plan = Planner.For(Shipped, new[] { S(1, Berry, 4) }, 2, true, true);

        Assert.NotNull(plan);
        Assert.Equal(3, plan!.Units);
        Assert.Equal(1, plan.Brew.Bottles);
    }

    [Fact]
    public void Between_two_foods_that_are_just_as_good_the_setting_picks_by_freshness()
    {
        var pool = new[] { Made(1, 901, 30, 10, 6, hours: 100), Made(2, 902, 30, 10, 6, hours: 5) };

        Assert.Equal(new[] { 902 }, Planner.For(Shipped, pool, 2, true, true)!.Takes.Select(take => take.Stack.ItemId));
        Assert.Equal(new[] { 901 }, Planner.For(Shipped, pool, 2, false, true)!.Takes.Select(take => take.Stack.ItemId));
    }

    [Fact]
    public void A_food_comes_from_one_storage_when_one_holds_enough()
    {
        // The chest's 10 spoil first, but the bag alone holds 23, so the plan is one line instead of two.
        var pool = new[] { S(1, Berry, 10, owner: Chest, hours: 5), S(2, Berry, 25, hours: 50) };
        var plan = Planner.For(Shipped, pool, 2, true, true);

        Assert.NotNull(plan);
        Assert.Equal(new[] { (2L, 23) }, plan!.Takes.Select(take => (take.Stack.InstanceId, take.Units)));
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
