using System;
using System.Collections.Generic;
using System.Text.Json;

namespace BrewingHub.Core;

/// <summary>What one warm-up run made, so a test can tell the real code ran.</summary>
public readonly record struct WarmUpResult(int Plans, int Nudges, int CatalogFoods, int Sends, int KeptStacks, bool HasCommand);

/// <summary>
/// The open brewing screen's plain code run once on a made-up cellar: the plans for each drink, the nudges, the
/// catalog and the state the page draws, the fingerprints that spare an unchanged read, and a page command. It
/// touches no game object, so it can run on a worker at load and leave the first real screen nothing to compile.
/// </summary>
public static class BrewWarmUp
{
    private const long Role = 1;
    private const long Chest = 50;
    private const long BarrelId = 700;
    private const int Berry = 101;
    private const int Mushroom = 102;
    private const int Potato = 103;

    public static WarmUpResult Run()
    {
        var catalog = MadeUpCatalog();
        var rules = Rules.Shipped;
        var stock = new List<Stack>
        {
            Of(catalog, 1, Berry, 25, Role, 40),
            Of(catalog, 2, Mushroom, 25, Chest, 30),
            Of(catalog, 3, Potato, 12, Chest, null),
            Of(catalog, 4, Berry, 3, Role, 0),
        };
        var contents = new List<Stack> { Of(catalog, 5, Potato, 1, BarrelId, null) };
        var mix = Mix.None.Add(contents[0], contents[0].Count);
        var barrels = new List<Barrel>
        {
            new(BarrelId, "Barrel", BarrelState.Idle, false, 0, 0, 0, 0, contents),
            new(701, "Cellar Barrel", BarrelState.Brewing, false, 202, 3, 10, 30, Array.Empty<Stack>()),
        };
        var cellar = new Cellar(Role, rules, catalog, stock, barrels, BarrelId, mix, true);
        var extras = new BrewExtras(new HashSet<long> { 701 }, true);

        var pool = new List<Stack>(stock);

        pool.AddRange(contents);

        var plans = 0;

        for (var tier = 0; tier < 3; tier++)
        {
            if (Planner.For(rules, pool, tier, true, true) != null) plans++;
        }

        var nudges = Nudges.For(rules, mix, contents, stock, true, true).Count;
        var sends = 0;
        var catalogJson = "";
        var feed = new StateFeed(json => { catalogJson = json; sends++; return true; }, _ => { sends++; return true; });

        feed.Send(cellar, extras);
        feed.Send(cellar, extras);
        feed.Send(cellar with { CanSplit = false }, extras with { IsSpoilingFirst = false });
        feed.Send(null, extras);
        StatePrint.OfHeld(stock, Role, barrels[0]);

        var memo = new StackMemo();

        memo.BeginSweep();
        memo.Keep(Berry, 0, 0, stock[0]);
        memo.EndSweep();
        memo.BeginSweep();

        var kept = memo.Find(stock[0].InstanceId, Berry, 0, 0, stock[0].Count, stock[0].OwnerId, stock[0].Place, stock[0].HoursLeft) == null ? 0 : 1;

        memo.EndSweep();

        var command = PageCommand.Parse(PageCommand.Prefix + "load|2") is PageCommand.Load;

        return new WarmUpResult(plans, nudges, CountFoods(catalogJson), sends, kept, command);
    }

    private static Stack Of(Catalog catalog, long instance, int item, int count, long owner, double? hours)
    {
        var food = catalog.Ingredients[item];

        return new Stack(instance, item, count, owner, owner == Role ? "Bag" : "Chest", hours, food.Sat, food.Mood);
    }

    /// <summary>Three foods, one for each drink on its own: a Poor one, an Average one and a Strong one.</summary>
    private static Catalog MadeUpCatalog() => new(
        new Dictionary<int, Ingredient>
        {
            [Berry] = new(Berry, "Berry", "Res/Icon/berry.png", 10, 6, false, 72),
            [Mushroom] = new(Mushroom, "Mushroom", null, 10, 2, false, 48),
            [Potato] = new(Potato, "Potato", null, 20, -4, true, null),
        },
        new[]
        {
            new Drink(0, 201, "Ale", null, "Thin and sour.", Array.Empty<Use>(), 0),
            new Drink(1, 202, "Beer", null, "A fair drink.", new[] { new Use(301, new[] { 302 }) }, 2),
            new Drink(2, 203, "Wine", "Res/Icon/wine.png", "Strong and sweet.", Array.Empty<Use>(), 0),
        },
        new Dictionary<int, (string, string?)> { [201] = ("Ale", null), [202] = ("Beer", null), [203] = ("Wine", null), [301] = ("Punch", null), [302] = ("Sugar", null) });

    private static int CountFoods(string catalogJson)
    {
        if (catalogJson.Length == 0) return 0;

        using var document = JsonDocument.Parse(catalogJson);

        return document.RootElement.GetProperty("ings").GetArrayLength();
    }
}
