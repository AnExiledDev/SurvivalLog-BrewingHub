using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace BrewingHub.Core;

/// <summary>What the session keeps per barrel that the game does not: whether it brews again when collected.</summary>
public sealed record BrewExtras(IReadOnlySet<long> Again, bool IsSpoilingFirst)
{
    public static readonly BrewExtras None = new(new HashSet<long>(), true);
}

/// <summary>
/// The JSON page.js draws on the game's own brewing screen. Names and icons live in the catalog, sent once per game
/// and language; the state names everything by id.
/// </summary>
public static class BrewView
{
    private const int UrgentHours = 24;

    public static string NoState() => Json.Object(json => json.WriteBoolean("show", false));

    /// <summary>
    /// The open barrel: the rules, what is in it and what it brews, the best barrel of each drink from the food in
    /// reach, the nudges, how much of each food is in reach, how many of each it takes to reach the next drink, and
    /// every barrel in the base.
    /// </summary>
    public static string State(Cellar cellar, BrewExtras extras) => Json.Object(json =>
    {
        var open = cellar.Open;
        var rules = cellar.Rules;
        var mix = cellar.OpenMix;
        var brew = BrewMath.Of(rules, mix);
        var contents = open?.Contents ?? Array.Empty<Stack>();

        json.WriteBoolean("show", open != null);
        json.WriteBoolean("spoilingFirst", extras.IsSpoilingFirst);
        json.WriteBoolean("canSplit", cellar.CanSplit);
        WriteRules(json, rules);

        if (open != null)
        {
            json.WriteStartObject("barrel");
            json.WriteString("id", Id(open.FurnitureId));
            json.WriteNumber("state", (int)open.State);
            json.WriteBoolean("locked", open.IsLocked);
            json.WriteBoolean("again", extras.Again.Contains(open.FurnitureId));
            json.WriteEndObject();
        }

        WriteBrew(json, "mix", brew);
        json.WriteStartArray("contents");

        foreach (var group in contents.GroupBy(stack => stack.ItemId))
        {
            json.WriteStartObject();
            json.WriteNumber("id", group.Key);
            json.WriteNumber("n", group.Sum(stack => stack.Count));
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("plans");

        var pool = cellar.Stock.Concat(contents).ToList();

        for (var tier = 0; tier < 3; tier++) WritePlan(json, Planner.For(rules, pool, tier, extras.IsSpoilingFirst, cellar.CanSplit));

        json.WriteEndArray();
        json.WriteStartArray("nudges");

        if (open is { State: BarrelState.Idle, IsLocked: false })
        {
            foreach (var nudge in Nudges.For(rules, mix, contents, cellar.Stock, extras.IsSpoilingFirst, cellar.CanSplit)) WriteNudge(json, nudge);
        }

        json.WriteEndArray();
        json.WriteStartObject("have");

        foreach (var group in cellar.Stock.Where(stack => !stack.IsSpoiled).GroupBy(stack => stack.ItemId)) json.WriteNumber(Id(group.Key), group.Sum(stack => stack.Count));

        json.WriteEndObject();
        WriteReach(json, cellar.Catalog, rules, mix, brew);
        json.WriteStartArray("barrels");

        foreach (var barrel in cellar.Barrels) WriteBarrel(json, cellar, barrel, extras);

        json.WriteEndArray();
    });

    /// <summary>Every brewable food, the drinks with what uses them, and names and icons of the other items the drinks name.</summary>
    public static string Catalog(Catalog catalog) => Json.Object(json =>
    {
        json.WriteStartArray("ings");

        foreach (var ingredient in catalog.Ingredients.Values.OrderBy(ingredient => ingredient.Id))
        {
            json.WriteStartObject();
            json.WriteNumber("id", ingredient.Id);
            json.WriteString("name", ingredient.Name);
            WriteIcon(json, ingredient.Icon);
            json.WriteNumber("sat", Round(ingredient.Sat));
            json.WriteNumber("mood", Round(ingredient.Mood));
            json.WriteBoolean("dish", ingredient.IsDish);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("drinks");

        foreach (var drink in catalog.Drinks)
        {
            json.WriteStartObject();
            json.WriteNumber("tier", drink.Tier);
            json.WriteNumber("id", drink.ItemId);
            json.WriteString("name", drink.Name);
            WriteIcon(json, drink.Icon);
            json.WriteString("des", drink.Description);
            json.WriteNumber("asks", drink.Asks);
            json.WriteStartArray("used");

            foreach (var use in drink.UsedIn)
            {
                json.WriteStartObject();
                json.WriteNumber("make", use.MakesId);
                json.WriteStartArray("with");

                foreach (var id in use.WithIds) json.WriteNumberValue(id);

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartObject("items");

        foreach (var (id, item) in catalog.Items)
        {
            json.WriteStartObject(Id(id));
            json.WriteString("name", item.Name);
            WriteIcon(json, item.Icon);
            json.WriteEndObject();
        }

        json.WriteEndObject();
    });

    private static void WriteRules(Utf8JsonWriter json, Rules rules)
    {
        json.WriteStartObject("rules");
        json.WriteNumber("low", Round(rules.Low));
        json.WriteNumber("high", Round(rules.High));
        json.WriteNumber("per", Round(rules.PerBottle));
        json.WriteNumber("max", rules.MaxBottles);
        json.WriteNumber("min", Round(rules.MinSatiety));
        json.WriteNumber("speed", Round(rules.SpeedCut));
        json.WriteEndObject();
    }

    private static void WriteBrew(Utf8JsonWriter json, string name, Brew brew)
    {
        json.WriteStartObject(name);
        json.WriteNumber("count", brew.Count);
        json.WriteNumber("sat", Round(brew.Sat));
        json.WriteNumber("mood", Round(brew.Mood));
        json.WriteNumber("q", Round(brew.Quality));
        json.WriteNumber("tier", brew.Tier);
        json.WriteNumber("bottles", brew.Bottles);
        json.WriteBoolean("canStart", brew.CanStart);
        json.WriteNumber("hours", brew.Hours);
        json.WriteNumber("wasted", Round(brew.Wasted));
        json.WriteNumber("next", Round(brew.NextBottle));
        json.WriteEndObject();
    }

    private static void WritePlan(Utf8JsonWriter json, Plan? plan)
    {
        if (plan == null)
        {
            json.WriteNullValue();

            return;
        }

        json.WriteStartObject();
        json.WriteNumber("tier", plan.Tier);
        json.WriteNumber("bottles", plan.Brew.Bottles);
        json.WriteNumber("q", Round(plan.Brew.Quality));
        json.WriteNumber("hours", plan.Brew.Hours);
        json.WriteNumber("wasted", Round(plan.Brew.Wasted));
        json.WriteStartArray("takes");

        foreach (var take in plan.Takes)
        {
            json.WriteStartObject();
            json.WriteNumber("id", take.Stack.ItemId);
            json.WriteNumber("n", take.Units);
            json.WriteString("where", take.Stack.Place);
            json.WriteBoolean("urgent", take.Stack.HoursLeft is < UrgentHours);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteNudge(Utf8JsonWriter json, Nudge nudge)
    {
        json.WriteStartObject();
        json.WriteString("kind", nudge.Kind == NudgeKind.Add ? "add" : "remove");
        json.WriteNumber("id", nudge.Stack.ItemId);
        json.WriteNumber("n", nudge.Units);
        json.WriteString("where", nudge.Stack.Place);
        json.WriteNumber("tier", nudge.Next.Tier);
        json.WriteNumber("bottles", nudge.Next.Bottles);
        json.WriteEndObject();
    }

    /// <summary>
    /// For each food: how many more lift the barrel to the next drink, or, once it is Strong, how many keep it Strong.
    /// A food that 20 do not lift is left out.
    /// </summary>
    private static void WriteReach(Utf8JsonWriter json, Catalog catalog, Rules rules, Mix mix, Brew brew)
    {
        var isTop = brew.Count > 0 && brew.Tier == 2;
        var target = brew.Count > 0 ? Math.Min(2, brew.Tier + 1) : 2;

        json.WriteNumber("reachTier", target);
        json.WriteBoolean("reachKeeps", isTop);
        json.WriteStartObject("reach");

        foreach (var ingredient in catalog.Ingredients.Values)
        {
            if (ingredient.Sat <= 0) continue;

            var n = isTop ? BrewMath.KeepsTop(rules, mix, ingredient.Sat, ingredient.Mood) : BrewMath.ToReach(rules, mix, ingredient.Sat, ingredient.Mood, target);

            if (n is { } count) json.WriteNumber(Id(ingredient.Id), count);
        }

        json.WriteEndObject();
    }

    private static void WriteBarrel(Utf8JsonWriter json, Cellar cellar, Barrel barrel, BrewExtras extras)
    {
        json.WriteStartObject();
        json.WriteString("id", Id(barrel.FurnitureId));
        json.WriteString("name", barrel.Name);
        json.WriteNumber("state", (int)barrel.State);
        json.WriteNumber("result", barrel.ResultItemId);
        json.WriteNumber("tier", cellar.Catalog.TierOf(barrel.ResultItemId) ?? -1);
        json.WriteNumber("bottles", barrel.ResultCount);
        json.WriteNumber("remain", Math.Max(0, barrel.RemainHours));
        json.WriteNumber("total", Math.Max(0, barrel.TotalHours));
        json.WriteBoolean("again", extras.Again.Contains(barrel.FurnitureId));
        json.WriteBoolean("open", barrel.FurnitureId == cellar.OpenId);
        json.WriteEndObject();
    }

    private static void WriteIcon(Utf8JsonWriter json, string? icon)
    {
        if (icon == null) json.WriteNull("icon");
        else json.WriteString("icon", icon);
    }

    /// <summary>Furniture ids pass 2^53, past what a JavaScript number holds exactly, so they travel as text.</summary>
    private static string Id(long id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static double Round(double value) => Math.Round(value, 3);
}

/// <summary>Writes one JSON object to a string.</summary>
internal static class Json
{
    public static string Object(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            body(json);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
