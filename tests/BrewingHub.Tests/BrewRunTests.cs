using BrewingHub.Core;
using Xunit;
using static BrewingHub.Tests.FakeGame;

namespace BrewingHub.Tests;

/// <summary>The presses as the player makes them, against a base kept in lists.</summary>
public class BrewRunTests
{
    private static FakeGame Stocked() => new()
    {
        Stock = { S(1, Berry, 20, hours: 30), S(2, Berry, 10, owner: Chest, hours: 5), S(3, Potato, 5) },
    };

    private static int InBarrel(FakeGame game, int item) => game.A.Contents.Where(s => s.ItemId == item).Sum(s => s.Count);

    [Fact]
    public void Load_fills_the_barrel_for_the_drink_from_the_bag_and_storage()
    {
        var game = Stocked();
        var pressed = BrewRun.Load(game, BarrelA, 2, true, false);

        Assert.Equal(new Pressed(true, "The barrel is filled for Wine."), pressed);
        Assert.Equal(23, InBarrel(game, Berry));
        Assert.Equal(0, InBarrel(game, Potato));
        Assert.DoesNotContain(game.Stock, s => s.OwnerId == Chest);
        Assert.Contains(BarrelA, game.ChangedIds);
    }

    [Fact]
    public void Load_that_moved_more_than_planned_is_not_reported_filled_and_does_not_start()
    {
        var game = Stocked();

        game.MovesWholeStacks = true;

        var pressed = BrewRun.Load(game, BarrelA, 2, true, false);

        Assert.False(pressed.IsDone);
        Assert.Equal("Moved 30 where 23 was planned; check the barrel before brewing.", pressed.Message);

        var again = Stocked();

        again.MovesWholeStacks = true;
        Assert.False(BrewRun.LoadAndStart(again, BarrelA, 2, true, false).IsDone);
        Assert.Empty(again.Started);
    }

    [Fact]
    public void Load_puts_what_is_in_the_barrel_back_in_the_bag_first()
    {
        var game = Stocked();

        game.A.Contents.Add(S(7, Potato, 3, owner: BarrelA));

        Assert.True(BrewRun.Load(game, BarrelA, 2, true, false).IsDone);
        Assert.Equal(0, InBarrel(game, Potato));
        Assert.Equal(8, game.Stock.Where(s => s.ItemId == Potato).Sum(s => s.Count));
    }

    [Fact]
    public void Load_counts_the_food_already_in_the_barrel_toward_the_plan()
    {
        var game = new FakeGame { Stock = { S(1, Berry, 2) } };

        game.A.Contents.Add(S(7, Berry, 2, owner: BarrelA));

        Assert.True(BrewRun.Load(game, BarrelA, 2, true, false).IsDone);
        Assert.Equal(4, InBarrel(game, Berry));
    }

    [Fact]
    public void Load_refuses_a_barrel_that_is_brewing_finished_or_locked()
    {
        var game = Stocked();

        game.A.State = BarrelState.Brewing;
        Assert.Equal(Texts.Busy.En, BrewRun.Load(game, BarrelA, 2, true, false).Message);

        game.A.Done(Wine, 4);
        Assert.Equal(Texts.CollectFirst.En, BrewRun.Load(game, BarrelA, 2, true, false).Message);

        game.A.State = BarrelState.Idle;
        game.A.IsLocked = true;
        Assert.Equal(Texts.Busy.En, BrewRun.Load(game, BarrelA, 2, true, false).Message);
        Assert.Empty(game.A.Contents);
    }

    [Fact]
    public void Load_without_enough_fresh_food_moves_nothing()
    {
        var game = new FakeGame { Stock = { S(1, Potato, 5) } };

        Assert.Equal(new Pressed(false, "Not enough fresh food for a barrel of Wine."), BrewRun.Load(game, BarrelA, 2, true, false));
        Assert.Empty(game.A.Contents);
    }

    [Fact]
    public void Load_says_how_much_moved_when_the_game_refuses_some()
    {
        var game = Stocked();

        game.RefusesMoves = true;

        Assert.Equal(new Pressed(false, "Moved 0 of 23; the game refused the rest."), BrewRun.Load(game, BarrelA, 2, true, false));
    }

    [Fact]
    public void Add_moves_one_unit_from_the_food_closest_to_spoiling()
    {
        var game = Stocked();

        Assert.Equal(new Pressed(true, "Added Berry from Chest."), BrewRun.Add(game, Berry, true, false));
        Assert.Equal(1, InBarrel(game, Berry));
        Assert.Equal(9, game.Stock.Single(s => s.OwnerId == Chest).Count);
    }

    [Fact]
    public void Add_with_none_left_says_so()
    {
        Assert.Equal(new Pressed(false, "No fresh Stew left."), BrewRun.Add(Stocked(), Stew, true, false));
    }

    [Fact]
    public void Remove_takes_one_unit_back_to_the_bag()
    {
        var game = Stocked();

        game.A.Contents.Add(S(7, Potato, 3, owner: BarrelA));

        Assert.Equal(new Pressed(true, "Took Potato out to your bag."), BrewRun.Remove(game, Potato, false));
        Assert.Equal(2, InBarrel(game, Potato));
        Assert.Equal(new Pressed(false, "No Berry in the barrel."), BrewRun.Remove(game, Berry, false));
    }

    [Fact]
    public void Presses_on_the_open_barrel_need_one_open()
    {
        var game = Stocked();

        game.OpenId = null;

        Assert.Equal(Texts.NoBarrel.En, BrewRun.Add(game, Berry, true, false).Message);
        Assert.Equal(Texts.NoBarrel.En, BrewRun.Empty(game, false).Message);
    }

    [Fact]
    public void Empty_sends_everything_to_the_bag()
    {
        var game = Stocked();

        Assert.Equal(Texts.AlreadyEmpty.En, BrewRun.Empty(game, false).Message);

        game.A.Contents.Add(S(7, Potato, 3, owner: BarrelA));
        game.A.Contents.Add(S(8, Berry, 2, owner: BarrelA));

        Assert.Equal(new Pressed(true, Texts.Emptied.En), BrewRun.Empty(game, false));
        Assert.Empty(game.A.Contents);
    }

    [Fact]
    public void Collect_takes_a_finished_barrels_bottles_and_passes_on_the_games_reason()
    {
        var game = Stocked();

        Assert.Equal(Texts.NotDone.En, BrewRun.Collect(game, BarrelB, false).Message);

        game.Barrels[1].Done(Wine, 4);
        game.CollectBlocked = "Your bag is full.";
        Assert.Equal(new Pressed(false, "Your bag is full."), BrewRun.Collect(game, BarrelB, false));

        game.CollectBlocked = null;
        Assert.Equal(new Pressed(true, "Collected 4 Wine."), BrewRun.Collect(game, BarrelB, false));
        Assert.Equal(Texts.NoBarrel.En, BrewRun.Collect(game, 999, false).Message);
    }

    [Fact]
    public void Collect_and_brew_collects_fills_and_starts_in_one_press()
    {
        var game = Stocked();

        game.A.Done(Wine, 4);

        Assert.Equal(new Pressed(true, "Collected 4 Wine. Started a barrel of Wine."), BrewRun.CollectAndBrew(game, BarrelA, 2, true, false));
        Assert.Equal(new[] { BarrelA }, game.Started);
        Assert.Equal(23, InBarrel(game, Berry));
    }

    [Fact]
    public void A_barrel_the_game_will_not_start_says_so_and_keeps_its_food()
    {
        var game = Stocked();

        game.RefusesStart = true;

        Assert.Equal(new Pressed(false, Texts.NotStarted.En), BrewRun.LoadAndStart(game, BarrelA, 2, true, false));
        Assert.Equal(23, InBarrel(game, Berry));
    }

    [Fact]
    public void Chinese_presses_answer_in_chinese()
    {
        Assert.Equal(Texts.NoGame.Zh, BrewRun.Empty(new FakeGame { HasGame = false }, true).Message);
    }
}
