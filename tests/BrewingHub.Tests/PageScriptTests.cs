using BrewingHub.Core;
using Xunit;
using static BrewingHub.Tests.FakeGame;

namespace BrewingHub.Tests;

/// <summary>The brewing screen as the player sees and uses it, driven through page.js in Jint.</summary>
public class PageScriptTests
{
    private static FakeGame Stocked() => new()
    {
        Stock = { S(1, Berry, 30), S(2, Mushroom, 20, owner: Chest, hours: 10), S(3, Stew, 2) },
    };

    private static string StateOf(FakeGame game, params long[] again) => BrewView.State(game.Cellar(), new BrewExtras(again.ToHashSet(), true));

    /// <summary>The player has the brewing screen open and the plugin has sent the catalog and this state.</summary>
    private static PageScript Opened(FakeGame game, params long[] again)
    {
        var page = new PageScript();

        page.OpenBrewing();
        page.Catalog(BrewView.Catalog(Book));
        page.Brew(StateOf(game, again));
        page.Poll();

        return page;
    }

    [Fact]
    public void The_script_says_it_is_ready_and_draws_nothing_until_a_barrel_is_open()
    {
        var page = new PageScript();

        page.OpenBrewing();
        page.Poll();

        Assert.Equal(1, page.Readies);
        Assert.False(page.Is("brewPage().getElementById('bhCol')"));

        page.Brew(StateOf(new FakeGame { OpenId = null }));
        page.Poll();

        Assert.Equal(0, page.Count("brewPage().querySelectorAll('.bh-ours')"));
    }

    [Fact]
    public void An_open_barrel_gets_the_hub_column_the_quality_line_and_the_drink_lines()
    {
        var game = Stocked();

        game.A.Contents.Add(S(7, Berry, 3, owner: BarrelA));

        var page = Opened(game);

        Assert.True(page.Is("brewPage().querySelector('.brew-system #bhCol')"));
        Assert.Contains("0.60", page.Text("shown('#bhQuality')"));
        Assert.Contains("best drink", page.Text("shown('#bhQuality')"));
        Assert.Contains("0.50+", page.Text("shown('#ql2')"));
        Assert.Contains("below 0.00", page.Text("shown('#ql0')"));
        Assert.Contains("Start now: ready in", page.Text("shown('#bhFerment')"));
        Assert.Contains("Empty to bag", page.Text("shown('#bhButtons')"));
    }

    [Fact]
    public void The_drink_lines_come_back_after_the_game_rewrites_its_labels()
    {
        var page = Opened(Stocked());

        page.Run("brewPage().getElementById('ql2').textContent = 'Strong'");
        page.Poll();

        Assert.Equal(1, page.Count("brewPage().querySelectorAll('#ql2 .bh-band')"));
    }

    [Fact]
    public void Load_asks_for_that_drink_once_until_the_press_is_answered()
    {
        var page = Opened(Stocked());

        page.Click("act('load', 2)");
        page.Click("act('load', 2)");

        Assert.Equal(new PageCommand[] { new PageCommand.Load(2) }, page.Sent);

        page.Status("The barrel is filled for Wine.");
        page.Click("act('load', 1)");

        Assert.Equal(new PageCommand.Load(1), page.Sent[^1]);
        Assert.Contains("The barrel is filled for Wine.", page.Text("document.getElementById('bh-toast').textContent"));
    }

    [Fact]
    public void Load_is_greyed_out_and_sends_nothing_while_the_barrel_brews()
    {
        var game = Stocked();

        game.A.State = BarrelState.Brewing;
        game.A.Remain = 30;
        game.A.Total = 72;

        var page = Opened(game);

        page.Click("act('load', 2)");

        Assert.True(page.Is("act('load', 2).disabled"));
        Assert.Empty(page.Sent);
        Assert.Contains("Ready in 1d 6h", page.Text("shown('#bhFerment')"));
    }

    [Fact]
    public void A_nudge_is_a_button_that_adds_or_takes_out_that_food()
    {
        var game = new FakeGame { Stock = { S(1, Stew, 1, owner: Chest) } };

        game.A.Contents.Add(S(7, Potato, 1, owner: BarrelA));
        game.A.Contents.Add(S(8, Berry, 3, owner: BarrelA));

        var page = Opened(game);

        Assert.Contains("Take out 1 Potato: Strong x1", page.Text("shown('#bhFerment')"));

        page.Click("act('remove', 103).querySelector('span')");

        Assert.Equal(new PageCommand.Remove(Potato), page.Sent.Single());
    }

    [Fact]
    public void A_finished_barrel_offers_collect_and_brew_again_and_the_switch()
    {
        var game = Stocked();

        game.A.Done(Wine, 4);

        var page = Opened(game);

        page.Click("act('again')");
        page.Status("");
        page.Click("act('rebrew').querySelector('.k')");

        Assert.Equal(new PageCommand[] { new PageCommand.CollectAgain(), new PageCommand.Rebrew(BarrelA, true) }, page.Sent);
    }

    [Fact]
    public void A_switch_that_is_on_turns_off()
    {
        var game = Stocked();

        game.A.State = BarrelState.Brewing;

        var page = Opened(game, BarrelA);

        page.Click("act('rebrew')");

        Assert.Equal(new PageCommand.Rebrew(BarrelA, false), page.Sent.Single());
    }

    [Fact]
    public void Collect_on_the_barrels_tab_names_the_barrel_by_its_exact_id()
    {
        var game = Stocked();

        game.OpenId = BarrelB;
        game.A.Done(Wine, 4);

        var page = Opened(game);

        page.Click("act('tab', 'barrels')");

        Assert.Contains("4 x Wine", page.Text("shown('#bhView')"));

        page.Click("act('collect')");

        Assert.Equal(new PageCommand.Collect(BarrelA), page.Sent.Single());
    }

    [Fact]
    public void The_ingredient_list_filters_by_search_and_a_row_adds_one()
    {
        var page = Opened(Stocked());

        page.Click("act('tab', 'ings')");

        Assert.Equal(3, page.Count("brewPage().querySelectorAll('#bhView .bh-trow[data-act]')"));

        page.Run("type(brewPage().getElementById('bhSearch'), 'berr')");

        Assert.Equal(new PageCommand.Focus(true), page.Sent.Single());
        Assert.Equal(1, page.Count("brewPage().querySelectorAll('#bhView .bh-trow[data-act]')"));

        page.Click("act('add', 101).querySelector('.nm')");

        Assert.Equal(new PageCommand.Add(Berry), page.Sent[^1]);
    }

    [Fact]
    public void Cooked_dishes_show_only_with_the_raw_food_filter_off()
    {
        var page = Opened(Stocked());

        page.Click("act('tab', 'ings')");
        Assert.False(page.Is("act('add', 104)"));

        page.Click("act('filter', 'rawOnly')");
        Assert.True(page.Is("act('add', 104)"));
    }

    [Fact]
    public void The_recipes_tab_lists_each_drink_with_its_band_and_uses()
    {
        var page = Opened(Stocked());

        page.Click("act('tab', 'recipes')");

        var text = page.Text("shown('#bhView')");

        Assert.Contains("Strong and sweet.", text);
        Assert.Contains("Punch", text);
        Assert.Contains("Asked for in 2 relief requests", text);
    }

    [Fact]
    public void Items_take_the_colour_of_the_drink_they_make_alone()
    {
        var page = Opened(Stocked());

        page.Run("addItem('bagGrid', 5001, 101, true); addItem('bagGrid', 5002, 103, true); addItem('vatGrid', 5003, 102, false)");
        page.Poll();

        Assert.True(page.Is("brewPage().getElementById('5001').classList.contains('bh-strong')"));
        Assert.True(page.Is("brewPage().getElementById('5002').classList.contains('bh-poor')"));
        Assert.True(page.Is("brewPage().getElementById('5003').classList.contains('bh-avg')"));
    }

    [Fact]
    public void The_item_tip_adds_morale_quality_and_the_drink_alone()
    {
        var page = Opened(Stocked());

        page.Run("brewFrame().contentWindow.showItemTip({ name: 'Berry', configId: 101 })");

        var tip = page.Text("shown('#itemTooltip')");

        Assert.Contains("Morale 6, quality 0.60", tip);
        Assert.Contains("Alone: Wine, 3 per bottle", tip);
    }

    [Fact]
    public void Closing_the_barrel_takes_everything_off_the_screen()
    {
        var page = Opened(Stocked());

        page.Run("addItem('bagGrid', 5001, 101, true)");
        page.Poll();
        page.Brew(BrewView.NoState());
        page.Poll();

        Assert.Equal(0, page.Count("brewPage().querySelectorAll('.bh-ours')"));
        Assert.False(page.Is("brewPage().getElementById('5001').classList.contains('bh-strong')"));
        Assert.True(page.Is("brewPage().getElementById('brewBtn')"));
    }

    [Fact]
    public void The_same_state_again_leaves_the_drawing_alone()
    {
        var game = Stocked();
        var page = Opened(game);

        page.Run("var before = brewPage().getElementById('bhView').children[0]");
        page.Brew(StateOf(game));
        page.Poll();

        Assert.True(page.Is("brewPage().getElementById('bhView').children[0] === before"));
    }

    [Fact]
    public void Scrolling_the_hub_list_tells_the_game_to_find_its_buttons_again()
    {
        var page = Opened(Stocked());

        page.Run("fire(brewPage().getElementById('bhView'), 'scroll')");

        Assert.Equal("1", page.Text("brewFrame().contentWindow.scrolls"));
    }
}
