using BrewingHub.Core;
using Xunit;

namespace BrewingHub.Tests;

/// <summary>The barrel's own sums: what a mix brews, how long it takes, and how many more of a food change the drink.</summary>
public class BrewMathTests
{
    private static readonly Rules Shipped = Rules.Shipped;

    [Theory]
    [InlineData(-0.01, 0)]
    [InlineData(0.0, 1)]
    [InlineData(0.49, 1)]
    [InlineData(0.5, 2)]
    public void The_quality_lines_start_the_better_drink_exactly_at_the_line(double quality, int tier)
    {
        Assert.Equal(tier, BrewMath.TierOf(Shipped, quality));
    }

    [Fact]
    public void A_mix_under_one_bottle_still_counts_one_bottle_and_the_cap_holds()
    {
        Assert.Equal(1, BrewMath.Of(Shipped, new Mix(10, 6, 1)).Bottles);
        Assert.Equal(9, BrewMath.Of(Shipped, new Mix(400, 100, 40)).Bottles);
        Assert.Equal(0, BrewMath.Of(Shipped, Mix.None).Bottles);
    }

    [Fact]
    public void A_brew_needs_the_least_satiety_to_start()
    {
        Assert.False(BrewMath.Of(Shipped, new Mix(24, 10, 3)).CanStart);
        Assert.True(BrewMath.Of(Shipped, new Mix(25, 10, 3)).CanStart);
    }

    [Fact]
    public void Satiety_past_the_last_whole_bottle_is_wasted_and_the_next_bottle_says_how_far()
    {
        var brew = BrewMath.Of(Shipped, new Mix(60, 30, 6));

        Assert.Equal(2, brew.Bottles);
        Assert.Equal(10, brew.Wasted, 6);
        Assert.Equal(15, brew.NextBottle, 6);
    }

    [Fact]
    public void A_full_barrel_has_no_next_bottle()
    {
        Assert.Equal(0, BrewMath.Of(Shipped, new Mix(230, 100, 23)).NextBottle);
    }

    [Fact]
    public void Hours_grow_with_satiety_up_to_the_cap_and_the_speed_talent_cuts_them()
    {
        Assert.Equal(72, BrewMath.Hours(Shipped, 100));
        Assert.Equal(168, BrewMath.Hours(Shipped, 400));
        Assert.Equal(36, BrewMath.Hours(Shipped with { SpeedCut = 0.5 }, 100));
    }

    [Fact]
    public void Hours_round_halves_to_even_as_the_game_does_and_never_drop_under_one()
    {
        var rules = Shipped with { HourBase = 0, HourPerSatiety = 0.5, HourMax = 100 };

        Assert.Equal(2, BrewMath.Hours(rules, 5));
        Assert.Equal(4, BrewMath.Hours(rules, 7));
        Assert.Equal(1, BrewMath.Hours(rules, 0));
    }

    [Fact]
    public void Per_bottle_rounds_up_to_whole_items()
    {
        Assert.Equal(3, BrewMath.PerBottle(Shipped, 10));
        Assert.Equal(1, BrewMath.PerBottle(Shipped, 25));
        Assert.Equal(0, BrewMath.PerBottle(Shipped, 0));
    }

    [Fact]
    public void To_reach_counts_the_items_that_lift_the_mix_or_gives_up()
    {
        var mix = new Mix(50, 0, 5);

        Assert.Equal(5, BrewMath.ToReach(Shipped, mix, 10, 10, 2));
        Assert.Null(BrewMath.ToReach(Shipped, mix, 10, 0, 2));
    }

    [Fact]
    public void Keeps_top_counts_how_many_go_in_before_the_mix_drops_out_of_strong()
    {
        Assert.Equal(5, BrewMath.KeepsTop(Shipped, new Mix(50, 50, 5), 10, 0));
        Assert.Equal(9, BrewMath.KeepsTop(Shipped, new Mix(50, 50, 5), 10, 10));
    }
}
