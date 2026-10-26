using BrewingHub.Core;
using Xunit;
using static BrewingHub.Tests.FakeGame;

namespace BrewingHub.Tests;

/// <summary>
/// What keeps the open brewing screen cheap: an unchanged read sends the page nothing, the catalog is built once per game
/// and language a slice at a time, the storages are read again only every few seconds, and a stack whose food did not
/// change is not asked of the game again. Each test also checks that the saving never hides a change.
/// </summary>
public class RefreshCostTests
{
    private static FakeGame Stocked() => new()
    {
        Stock = { S(1, Berry, 30), S(2, Mushroom, 20, owner: Chest, hours: 10), S(3, Stew, 2) },
    };

    private static IEnumerable<bool> Steps(List<string> done)
    {
        foreach (var name in new[] { "productions", "requests", "names", "foods", "icons" })
        {
            done.Add(name);

            yield return true;
        }
    }

    [Fact]
    public void A_job_cut_into_the_smallest_slices_ends_with_what_one_long_slice_does()
    {
        var whole = new List<string>();
        var sliced = new List<string>();

        Assert.True(new SlicedJob(Steps(whole)).Advance(() => false));

        var job = new SlicedJob(Steps(sliced));
        var slices = 1;

        while (!job.Advance(() => true)) slices++;

        Assert.Equal(whole, sliced);
        Assert.True(slices >= whole.Count, $"{slices} slices for {whole.Count} steps");
    }

    [Fact]
    public void A_step_waiting_on_other_work_holds_the_rest_for_the_next_frame()
    {
        var isReady = false;
        var done = new List<string>();

        IEnumerable<bool> Waiting()
        {
            done.Add("read");

            while (!isReady) yield return false;

            done.Add("build");

            yield return true;
        }

        var job = new SlicedJob(Waiting());

        Assert.False(job.Advance(() => false));
        Assert.False(job.Advance(() => false));
        Assert.Equal(new[] { "read" }, done);

        isReady = true;

        Assert.True(job.Advance(() => false));
        Assert.Equal(new[] { "read", "build" }, done);
    }

    /// <summary>The game tables for three drinks, a recipe that takes Beer, two requests and a few foods.</summary>
    private sealed class Tables : ICatalogTables
    {
        public IReadOnlyList<int> Drinks { get; init; } = new[] { Ale, Beer, Wine };

        public IconIndex? Icons { get; set; } = IconIndex.Build(new[] { "Res/Icon/berry.png", "Res/Icon/wine.png" });

        public string WineName { get; init; } = "Wine";

        public bool Breaks { get; init; }

        public IEnumerable<IReadOnlyList<int>> RecipeResults()
        {
            yield return new[] { Stew };
        }

        public IEnumerable<FoodRow?> Foods()
        {
            if (Breaks) throw new InvalidOperationException("the item table is gone");

            yield return new FoodRow(Berry, "Berry", "../../Res/Icon/berry.png", 10, 6, 72);
            yield return null;
            yield return new FoodRow(Stew, "Stew", null, 30, 15, 24);
            yield return new FoodRow(50001, "Pie", null, 25, 10, null);
        }

        public IEnumerable<(IReadOnlyList<int> Materials, IReadOnlyList<int> Products)> Productions()
        {
            yield return (new[] { Beer, 302 }, new[] { 301 });
            yield return (new[] { Wine }, new[] { 303 });
        }

        public IEnumerable<IReadOnlyList<int>> Requests()
        {
            yield return new[] { Beer };
            yield return new[] { Beer, Wine };
        }

        public (string Name, string? IconName)? Item(int id) => id switch
        {
            Ale => ("Ale", null),
            Beer => ("Beer", null),
            Wine => (WineName, "../../Res/Icon/wine.png"),
            301 => ("Punch", null),
            302 => ("Sugar", null),
            _ => null,
        };

        public string Description(int id) => id == Wine ? "Strong and sweet." : "";
    }

    private static Catalog BuildAtOnce(Tables tables)
    {
        var build = new CatalogBuild(tables);

        Assert.True(new SlicedJob(build.Steps()).Advance(() => false));

        return build.Result!;
    }

    [Fact]
    public void Built_in_the_smallest_slices_the_catalog_is_the_one_a_single_slice_builds()
    {
        var whole = BuildAtOnce(new Tables());
        var build = new CatalogBuild(new Tables());
        var job = new SlicedJob(build.Steps());
        var slices = 1;

        while (!job.Advance(() => true)) slices++;

        Assert.Equal(BrewView.Catalog(whole), BrewView.Catalog(build.Result!));
        Assert.True(slices > 10, $"{slices} slices");
    }

    [Fact]
    public void The_catalog_waits_for_the_web_icons_before_it_is_put_together()
    {
        var tables = new Tables { Icons = null };
        var build = new CatalogBuild(tables);
        var job = new SlicedJob(build.Steps());

        Assert.False(job.Advance(() => false));
        Assert.False(job.Advance(() => false));
        Assert.Null(build.Result);

        tables.Icons = IconIndex.Build(new[] { "Res/Icon/berry.png" });

        Assert.True(job.Advance(() => false));
        Assert.Equal("Res/Icon/berry.png", build.Result!.Ingredients[Berry].Icon);
    }

    [Fact]
    public void Dishes_recipes_requests_and_names_come_from_the_tables()
    {
        var catalog = BuildAtOnce(new Tables());

        Assert.Equal(new[] { Berry, Stew, 50001 }, catalog.Ingredients.Keys.OrderBy(id => id));
        Assert.False(catalog.Ingredients[Berry].IsDish);
        Assert.True(catalog.Ingredients[Stew].IsDish);
        Assert.True(catalog.Ingredients[50001].IsDish);

        Assert.Equal(new[] { Ale, Beer, Wine }, catalog.Drinks.Select(drink => drink.ItemId));
        Assert.Equal(new[] { 0, 2, 1 }, catalog.Drinks.Select(drink => drink.Asks));

        var punch = Assert.Single(catalog.Drinks[1].UsedIn);

        Assert.Equal(301, punch.MakesId);
        Assert.Equal(new[] { 302 }, punch.WithIds);
        Assert.Equal("Sugar", catalog.Items[302].Name);
        Assert.Equal("Item 303", catalog.Items[303].Name);
        Assert.Equal("Res/Icon/wine.png", catalog.Drinks[2].Icon);
        Assert.Equal("Strong and sweet.", catalog.Drinks[2].Description);
    }

    private static Catalog Finish<TKey>(CatalogCache<TKey> cache)
    {
        Catalog? built = null;

        while (cache.IsBuilding) built = cache.Advance(() => false) ?? built;

        return built!;
    }

    private static Func<CatalogBuild> Never => () => throw new InvalidOperationException("built again");

    [Fact]
    public void The_same_game_and_language_builds_the_catalog_once()
    {
        var cache = new CatalogCache<string>();
        var starts = 0;

        Assert.Null(cache.For("en", true, () =>
        {
            starts++;

            return new CatalogBuild(new Tables());
        }));

        var built = cache.Advance(() => false);

        Assert.NotNull(built);
        Assert.Same(built, cache.For("en", true, Never));
        Assert.Same(built, cache.For("en", false, Never));
        Assert.False(cache.IsBuilding);
        Assert.Equal(1, starts);
    }

    [Fact]
    public void A_new_language_builds_the_catalog_again_and_the_old_one_stands_until_it_is_done()
    {
        var cache = new CatalogCache<string>();

        cache.For("en", true, () => new CatalogBuild(new Tables()));

        var english = Finish(cache);

        Assert.Same(english, cache.For("fr", true, () => new CatalogBuild(new Tables { WineName = "Vin" })));
        Assert.True(cache.IsBuilding);
        Assert.Same(english, cache.For("fr", true, Never));

        var french = Finish(cache);

        Assert.Equal("Vin", french.Drinks[2].Name);
        Assert.Same(french, cache.For("fr", true, Never));
        Assert.Null(cache.Built("en", true));
    }

    [Fact]
    public void A_language_change_during_a_build_starts_it_again_for_the_new_language()
    {
        var cache = new CatalogCache<string>();

        cache.For("en", true, () => new CatalogBuild(new Tables()));
        cache.Advance(() => true);

        Assert.True(cache.IsBuilding);

        cache.For("fr", true, () => new CatalogBuild(new Tables { WineName = "Vin" }));

        var built = Finish(cache);

        Assert.Equal("Vin", built.Drinks[2].Name);
        Assert.Same(built, cache.Built("fr", true));
        Assert.Null(cache.Built("en", true));
    }

    [Fact]
    public void A_catalog_built_before_any_barrel_stood_is_built_again_once_one_does()
    {
        var cache = new CatalogCache<string>();

        cache.For("en", false, () => new CatalogBuild(new Tables { Drinks = Array.Empty<int>() }));

        var bare = Finish(cache);

        Assert.Empty(bare.Drinks);
        Assert.Same(bare, cache.For("en", false, Never));
        Assert.Same(bare, cache.For("en", true, () => new CatalogBuild(new Tables())));

        var full = Finish(cache);

        Assert.Equal(3, full.Drinks.Count);
        Assert.Same(full, cache.For("en", true, Never));
    }

    [Fact]
    public void A_build_that_fails_keeps_the_last_catalog_and_the_next_ask_tries_again()
    {
        var cache = new CatalogCache<string>();

        cache.For("en", true, () => new CatalogBuild(new Tables()));

        var english = Finish(cache);

        cache.For("fr", true, () => new CatalogBuild(new Tables { Breaks = true }));

        Assert.Throws<InvalidOperationException>(() => cache.Advance(() => false));
        Assert.False(cache.IsBuilding);
        Assert.Same(english, cache.For("fr", true, () => new CatalogBuild(new Tables { WineName = "Vin" })));
        Assert.True(cache.IsBuilding);
        Assert.Equal("Vin", Finish(cache).Drinks[2].Name);
    }

    /// <summary>The page end of the feed: what got through, and whether each kind of message gets through now.</summary>
    private sealed class Pipe
    {
        public List<string> Catalogs { get; } = new();

        public List<string> States { get; } = new();

        public bool IsCatalogDown { get; set; }

        public bool IsStateDown { get; set; }

        public StateFeed Feed() => new(json => Take(Catalogs, json, IsCatalogDown), json => Take(States, json, IsStateDown));

        private static bool Take(List<string> sent, string json, bool isDown)
        {
            if (isDown) return false;

            sent.Add(json);

            return true;
        }
    }

    [Fact]
    public void An_unchanged_read_sends_the_page_nothing()
    {
        var game = Stocked();
        var pipe = new Pipe();
        var feed = pipe.Feed();

        feed.Send(game.Cellar(), BrewExtras.None);
        feed.Send(game.Cellar(), BrewExtras.None);
        feed.Send(game.Cellar(), BrewExtras.None);

        Assert.Equal(BrewView.Catalog(Book), Assert.Single(pipe.Catalogs));
        Assert.Equal(BrewView.State(game.Cellar(), BrewExtras.None), Assert.Single(pipe.States));
    }

    [Fact]
    public void A_changed_read_sends_the_new_state_and_not_the_catalog_again()
    {
        var game = Stocked();
        var pipe = new Pipe();
        var feed = pipe.Feed();

        feed.Send(game.Cellar(), BrewExtras.None);
        game.Stock[0] = S(1, Berry, 31);
        feed.Send(game.Cellar(), BrewExtras.None);

        Assert.Single(pipe.Catalogs);
        Assert.Equal(2, pipe.States.Count);
        Assert.Equal(BrewView.State(game.Cellar(), BrewExtras.None), pipe.States[^1]);
        Assert.NotEqual(pipe.States[0], pipe.States[1]);
    }

    [Fact]
    public void Forgetting_sends_the_state_again_and_a_new_page_or_catalog_gets_both()
    {
        var game = Stocked();
        var pipe = new Pipe();
        var feed = pipe.Feed();

        feed.Send(game.Cellar(), BrewExtras.None);
        feed.Forget();
        feed.Send(game.Cellar(), BrewExtras.None);

        Assert.Equal((1, 2), (pipe.Catalogs.Count, pipe.States.Count));

        feed.ForgetCatalog();
        feed.Send(game.Cellar(), BrewExtras.None);

        Assert.Equal((2, 3), (pipe.Catalogs.Count, pipe.States.Count));

        var french = Book with { Drinks = Book.Drinks.Select(drink => drink with { Name = drink.Name + " fr" }).ToList() };

        feed.Send(game.Cellar() with { Catalog = french }, BrewExtras.None);

        Assert.Equal((3, 4), (pipe.Catalogs.Count, pipe.States.Count));
        Assert.Contains("Wine fr", pipe.Catalogs[^1]);
    }

    [Fact]
    public void A_closed_screen_is_sent_once_and_opening_it_again_sends_the_state()
    {
        var game = Stocked();
        var pipe = new Pipe();
        var feed = pipe.Feed();

        feed.Send(game.Cellar(), BrewExtras.None);
        feed.Send(null, BrewExtras.None);
        feed.Send(null, BrewExtras.None);

        Assert.Equal(2, pipe.States.Count);
        Assert.Equal(BrewView.NoState(), pipe.States[1]);

        feed.Send(game.Cellar(), BrewExtras.None);

        Assert.Equal(3, pipe.States.Count);
        Assert.Equal(pipe.States[0], pipe.States[2]);
    }

    [Fact]
    public void A_catalog_that_does_not_get_through_holds_the_state_and_both_go_on_the_next_read()
    {
        var game = Stocked();
        var pipe = new Pipe { IsCatalogDown = true };
        var feed = pipe.Feed();

        feed.Send(game.Cellar(), BrewExtras.None);

        Assert.Empty(pipe.States);

        pipe.IsCatalogDown = false;
        feed.Send(game.Cellar(), BrewExtras.None);

        Assert.Single(pipe.Catalogs);
        Assert.Single(pipe.States);
    }

    [Fact]
    public void A_state_that_does_not_get_through_is_sent_again_on_the_next_read()
    {
        var game = Stocked();
        var pipe = new Pipe { IsStateDown = true };
        var feed = pipe.Feed();

        feed.Send(game.Cellar(), BrewExtras.None);
        pipe.IsStateDown = false;
        feed.Send(game.Cellar(), BrewExtras.None);

        Assert.Single(pipe.Catalogs);
        Assert.Single(pipe.States);
    }

    [Fact]
    public void Two_reads_of_the_same_base_print_alike()
    {
        var game = Stocked();

        game.A.Contents.Add(S(7, Berry, 3, owner: BarrelA));

        Assert.Equal(StatePrint.Of(game.Cellar(), BrewExtras.None), StatePrint.Of(game.Cellar(), BrewExtras.None));
    }

    [Fact]
    public void Anything_the_screen_shows_changes_the_print()
    {
        var changes = new (string Name, Action<FakeGame> Change, BrewExtras? Extras)[]
        {
            ("one more berry", game => game.Stock[0] = S(1, Berry, 31), null),
            ("a stack moved to the bag", game => game.Stock[1] = S(2, Mushroom, 20, hours: 10), null),
            ("a stack closer to spoiling", game => game.Stock[1] = S(2, Mushroom, 20, owner: Chest, hours: 9), null),
            ("food in the open barrel", game => game.A.Contents.Add(S(7, Berry, 1, owner: BarrelA)), null),
            ("the open barrel brewing", game => game.A.State = BarrelState.Brewing, null),
            ("another barrel finished", game => game.Barrels[1].Done(Wine, 4), null),
            ("another barrel open", game => game.OpenId = BarrelB, null),
            ("a setting", game => game.Rules = game.Rules with { PerBottle = game.Rules.PerBottle + 1 }, null),
            ("no splitting", game => game.CanSplit = false, null),
            ("brew again on", _ => { }, new BrewExtras(new HashSet<long> { BarrelA }, true)),
            ("spoiling first off", _ => { }, new BrewExtras(new HashSet<long>(), false)),
        };

        var before = StatePrint.Of(Stocked().Cellar(), BrewExtras.None);

        foreach (var (name, change, extras) in changes)
        {
            var game = Stocked();

            change(game);

            Assert.True(StatePrint.Of(game.Cellar(), extras ?? BrewExtras.None) != before, name);
        }
    }

    [Fact]
    public void Food_moved_by_hand_in_the_bag_or_the_open_barrel_changes_the_held_print()
    {
        var game = Stocked();

        ulong Held()
        {
            var cellar = game.Cellar();

            return StatePrint.OfHeld(cellar.Stock, Role, cellar.Open);
        }

        var before = Held();

        game.Stock[1] = S(2, Mushroom, 5, owner: Chest, hours: 10);

        Assert.Equal(before, Held());

        game.Stock[0] = S(1, Berry, 29);

        var bagChanged = Held();

        Assert.NotEqual(before, bagChanged);

        game.A.Contents.Add(S(7, Berry, 1, owner: BarrelA));

        Assert.NotEqual(bagChanged, Held());
    }

    [Fact]
    public void Storage_is_read_again_every_few_seconds_on_the_hour_and_after_a_press()
    {
        var clock = new FullReadClock();

        Assert.True(clock.IsDue(10, 8));

        clock.Read(10, 8);

        Assert.False(clock.IsDue(11, 8));
        Assert.True(clock.IsDue(13, 8));
        Assert.True(clock.IsDue(11, 9));
        Assert.True(clock.IsDue(5, 8));

        clock.Forget();

        Assert.True(clock.IsDue(10.5, 8));
    }

    [Fact]
    public void A_kept_stack_is_reused_until_its_food_changes()
    {
        var memo = new StackMemo();
        var kept = memo.Keep(Berry, 0, 3, S(1, Berry, 30));

        Assert.Same(kept, memo.Find(1, Berry, 0, 3, 30, Role, "Bag", null));

        var moved = memo.Find(1, Berry, 0, 3, 12, Chest, "Chest", 5);

        Assert.Equal(S(1, Berry, 12, owner: Chest, hours: 5), moved);
        Assert.Same(moved, memo.Find(1, Berry, 0, 3, 12, Chest, "Chest", 5));
        Assert.Null(memo.Find(1, Berry, 1, 3, 12, Chest, "Chest", 5));
        Assert.Null(memo.Find(1, Mushroom, 0, 3, 12, Chest, "Chest", 5));
        Assert.Null(memo.Find(2, Berry, 0, 3, 12, Chest, "Chest", 5));
    }

    [Fact]
    public void A_full_read_drops_stacks_it_did_not_meet_and_leaving_the_game_drops_all()
    {
        var memo = new StackMemo();

        memo.Keep(Berry, 0, 3, S(1, Berry, 30));
        memo.Keep(Mushroom, 0, 3, S(2, Mushroom, 20));
        memo.BeginSweep();
        memo.Find(1, Berry, 0, 3, 30, Role, "Bag", null);
        memo.Keep(Potato, 0, 1, S(3, Potato, 4));
        memo.EndSweep();

        Assert.NotNull(memo.Find(1, Berry, 0, 3, 30, Role, "Bag", null));
        Assert.NotNull(memo.Find(3, Potato, 0, 1, 4, Role, "Bag", null));
        Assert.Null(memo.Find(2, Mushroom, 0, 3, 20, Role, "Bag", null));

        memo.Forget();

        Assert.Null(memo.Find(1, Berry, 0, 3, 30, Role, "Bag", null));
    }
}
