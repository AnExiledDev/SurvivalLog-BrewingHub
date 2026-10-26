using System.Collections.Generic;
using System.Linq;

namespace BrewingHub.Core;

/// <summary>
/// The game tables the catalog is read from, one row at a time, so the read can be spread over frames. Every list is
/// in the table's own order.
/// </summary>
public interface ICatalogTables
{
    /// <summary>The barrel's drinks by tier; empty when no barrel stands.</summary>
    IReadOnlyList<int> Drinks { get; }

    /// <summary>The items each cooking recipe can turn out.</summary>
    IEnumerable<IReadOnlyList<int>> RecipeResults();

    /// <summary>Every item row: the brewable ones read out, the others null, so a long run of them still lets a frame end.</summary>
    IEnumerable<FoodRow?> Foods();

    /// <summary>Every workbench recipe: what it takes and what it makes.</summary>
    IEnumerable<(IReadOnlyList<int> Materials, IReadOnlyList<int> Products)> Productions();

    /// <summary>The items each relief request asks for.</summary>
    IEnumerable<IReadOnlyList<int>> Requests();

    /// <summary>An item's name and the icon its row names, or null when the table has no such item.</summary>
    (string Name, string? IconName)? Item(int id);

    /// <summary>An item's description, or empty.</summary>
    string Description(int id);

    /// <summary>The game's web icons, or null while they are still being found.</summary>
    IconIndex? Icons { get; }
}

/// <summary>
/// Builds the catalog a row at a time (run it as a SlicedJob): the recipes that take each drink, the requests that ask
/// for it, the names of everything they name, which foods are dishes, and every brewable food. It waits for the web
/// icons last, then puts the catalog together. Sliced or run at once, it builds the same catalog.
/// </summary>
public sealed class CatalogBuild
{
    private const int DishIdsFrom = 50000;

    private readonly ICatalogTables _tables;

    public CatalogBuild(ICatalogTables tables)
    {
        _tables = tables;
    }

    /// <summary>The catalog, once the steps have all run; null before.</summary>
    public Catalog? Result { get; private set; }

    public IEnumerable<bool> Steps()
    {
        var drinkIds = _tables.Drinks;
        var uses = new List<Use>[drinkIds.Count];
        var asks = new int[drinkIds.Count];

        for (var tier = 0; tier < drinkIds.Count; tier++) uses[tier] = new List<Use>();

        foreach (var (materials, products) in _tables.Productions())
        {
            for (var tier = 0; tier < drinkIds.Count; tier++)
            {
                var drinkId = drinkIds[tier];

                if (products.Count > 0 && materials.Contains(drinkId)) uses[tier].Add(new Use(products[0], materials.Where(id => id != drinkId).ToList()));
            }

            yield return true;
        }

        foreach (var demands in _tables.Requests())
        {
            for (var tier = 0; tier < drinkIds.Count; tier++)
            {
                if (demands.Contains(drinkIds[tier])) asks[tier]++;
            }

            yield return true;
        }

        var named = new Dictionary<int, (string Name, string? IconName)>();
        var descriptions = new string[drinkIds.Count];

        for (var tier = 0; tier < drinkIds.Count; tier++)
        {
            Name(named, drinkIds[tier]);
            descriptions[tier] = _tables.Description(drinkIds[tier]);

            yield return true;

            foreach (var use in uses[tier])
            {
                Name(named, use.MakesId);

                foreach (var with in use.WithIds) Name(named, with);

                yield return true;
            }
        }

        var dishes = new HashSet<int>();

        foreach (var results in _tables.RecipeResults())
        {
            foreach (var id in results) dishes.Add(id);

            yield return true;
        }

        var foods = new List<FoodRow>();

        foreach (var food in _tables.Foods())
        {
            if (food != null) foods.Add(food);

            yield return true;
        }

        while (_tables.Icons == null) yield return false;

        var icons = _tables.Icons ?? IconIndex.Empty;
        var ingredients = new Dictionary<int, Ingredient>();

        foreach (var food in foods)
        {
            ingredients[food.Id] = new Ingredient(food.Id, food.Name, icons.Resolve(food.IconName), food.Sat, food.Mood, dishes.Contains(food.Id) || food.Id >= DishIdsFrom, food.LifeHours);
        }

        var items = new Dictionary<int, (string Name, string? Icon)>();

        foreach (var (id, item) in named) items[id] = (item.Name, icons.Resolve(item.IconName));

        var drinks = new List<Drink>();

        for (var tier = 0; tier < drinkIds.Count; tier++)
        {
            var item = items[drinkIds[tier]];

            drinks.Add(new Drink(tier, drinkIds[tier], item.Name, item.Icon, descriptions[tier], uses[tier], asks[tier]));
        }

        Result = new Catalog(ingredients, drinks, items);

        yield return true;
    }

    private void Name(Dictionary<int, (string Name, string? IconName)> named, int id)
    {
        if (named.ContainsKey(id)) return;

        named[id] = _tables.Item(id) ?? ($"Item {id}", null);
    }
}
