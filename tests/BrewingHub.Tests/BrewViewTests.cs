using System.Text.Json;
using BrewingHub.Core;
using Xunit;
using static BrewingHub.Tests.FakeGame;

namespace BrewingHub.Tests;

/// <summary>The JSON the page draws from.</summary>
public class BrewViewTests
{
    private static JsonElement State(FakeGame game, params long[] again) =>
        JsonDocument.Parse(BrewView.State(game.Cellar(), new BrewExtras(again.ToHashSet(), true))).RootElement;

    [Fact]
    public void Furniture_ids_travel_as_text_so_javascript_keeps_them_exact()
    {
        var state = State(new FakeGame(), BarrelA);

        Assert.Equal("9007199254740993", state.GetProperty("barrel").GetProperty("id").GetString());
        Assert.True(state.GetProperty("barrel").GetProperty("again").GetBoolean());
        Assert.Equal("9007199254740993", state.GetProperty("barrels")[0].GetProperty("id").GetString());
        Assert.True(state.GetProperty("barrels")[0].GetProperty("open").GetBoolean());
    }

    [Fact]
    public void No_open_barrel_shows_nothing()
    {
        Assert.False(State(new FakeGame { OpenId = null }).GetProperty("show").GetBoolean());
        Assert.False(JsonDocument.Parse(BrewView.NoState()).RootElement.GetProperty("show").GetBoolean());
    }

    [Fact]
    public void There_is_a_plan_slot_for_each_drink_with_null_for_one_out_of_reach()
    {
        var plans = State(new FakeGame { Stock = { S(1, Berry, 30) } }).GetProperty("plans");

        Assert.Equal(3, plans.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, plans[0].ValueKind);
        Assert.Equal(9, plans[2].GetProperty("bottles").GetInt32());
        Assert.Equal("Bag", plans[2].GetProperty("takes")[0].GetProperty("where").GetString());
    }

    [Fact]
    public void A_brewing_barrel_gets_no_nudges()
    {
        var game = new FakeGame { Stock = { S(1, Stew, 1) } };

        game.A.Contents.Add(S(7, Potato, 2, owner: BarrelA));
        Assert.NotEqual(0, State(game).GetProperty("nudges").GetArrayLength());

        game.A.State = BarrelState.Brewing;
        Assert.Equal(0, State(game).GetProperty("nudges").GetArrayLength());
    }

    [Fact]
    public void Have_counts_only_fresh_food_and_reach_names_the_next_drink()
    {
        var game = new FakeGame { Stock = { S(1, Berry, 4), S(2, Berry, 3, owner: Chest), S(3, Potato, 9, hours: 0) } };

        game.A.Contents.Add(S(7, Mushroom, 5, owner: BarrelA));

        var state = State(game);

        Assert.Equal(7, state.GetProperty("have").GetProperty("101").GetInt32());
        Assert.False(state.GetProperty("have").TryGetProperty("103", out _));
        Assert.Equal(2, state.GetProperty("reachTier").GetInt32());
        Assert.Equal(15, state.GetProperty("reach").GetProperty("101").GetInt32());
    }
}
