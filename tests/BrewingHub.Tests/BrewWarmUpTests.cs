using BrewingHub.Core;
using Xunit;

namespace BrewingHub.Tests;

/// <summary>
/// The warm-up only helps if it runs the code the first brewing screen runs: a plan for every drink, a nudge, the
/// catalog and state sent once and an unchanged read sent nothing, a stack kept and found again, and a page command.
/// A warm-up that quietly skips half of that leaves the first screen to compile it on the game's frame again.
/// </summary>
public class BrewWarmUpTests
{
    [Fact]
    public void The_made_up_cellar_goes_through_planning_the_page_feed_and_the_stack_memo()
    {
        var result = BrewWarmUp.Run();

        Assert.Equal(3, result.Plans);
        Assert.Equal(1, result.Nudges);
        Assert.Equal(3, result.CatalogFoods);
        Assert.Equal(4, result.Sends);
        Assert.Equal(1, result.KeptStacks);
        Assert.True(result.HasCommand);
    }

    [Fact]
    public void Two_runs_make_the_same_result_so_nothing_is_left_over_between_them()
    {
        Assert.Equal(BrewWarmUp.Run(), BrewWarmUp.Run());
    }
}
