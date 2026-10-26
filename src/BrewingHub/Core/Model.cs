using System;
using System.Collections.Generic;

namespace BrewingHub.Core;

/// <summary>
/// The barrel's rules as the game reads them, talents included: the two quality lines, satiety per bottle, the bottle
/// cap, the least satiety a brew needs, and the hours a brew takes (a base, more per satiety, a cap, and the brewing
/// speed talent's cut).
/// </summary>
public sealed record Rules(
    double Low,
    double High,
    double PerBottle,
    int MaxBottles,
    double MinSatiety,
    double HourBase,
    double HourPerSatiety,
    double HourMax,
    double SpeedCut)
{
    /// <summary>The values the game ships with and falls back to (Brew_* global settings, no talents).</summary>
    public static readonly Rules Shipped = new(0, 0.5, 25, 9, 25, 24, 0.48, 168, 0);

    public double Capacity => MaxBottles * PerBottle;
}

/// <summary>
/// One brewable food as the item table gives it: satiety and morale for a whole item (every use of an item used a
/// little at a time), whether it is a cooked dish, and the hours it keeps (null when it never spoils).
/// </summary>
public sealed record Ingredient(int Id, string Name, string? Icon, double Sat, double Mood, bool IsDish, double? LifeHours)
{
    public double Quality => Sat > 0 ? Mood / Sat : 0;
}

/// <summary>
/// One brewable row of the item table as the catalog reads it: name, the icon the table names, satiety and morale for a
/// whole item, and the hours it keeps (null when it never spoils).
/// </summary>
public sealed record FoodRow(int Id, string Name, string? IconName, double Sat, double Mood, double? LifeHours);

/// <summary>A drink the barrel makes: its tier (0 Poor, 1 Average, 2 Strong) and its item.</summary>
public sealed record Drink(int Tier, int ItemId, string Name, string? Icon, string Description, IReadOnlyList<Use> UsedIn, int Asks);

/// <summary>A workbench recipe that takes a drink: what it makes, and the other materials it needs.</summary>
public sealed record Use(int MakesId, IReadOnlyList<int> WithIds);

/// <summary>
/// The tables the views draw on, read once per game and language: every brewable food, the drinks, and item names and
/// icons for anything else the views mention.
/// </summary>
public sealed record Catalog(IReadOnlyDictionary<int, Ingredient> Ingredients, IReadOnlyList<Drink> Drinks, IReadOnlyDictionary<int, (string Name, string? Icon)> Items)
{
    public static readonly Catalog Empty = new(new Dictionary<int, Ingredient>(), Array.Empty<Drink>(), new Dictionary<int, (string, string?)>());

    public Drink? DrinkOf(int tier) => tier >= 0 && tier < Drinks.Count ? Drinks[tier] : null;

    /// <summary>The tier of a drink item, or null when the barrel does not make it.</summary>
    public int? TierOf(int itemId)
    {
        for (var i = 0; i < Drinks.Count; i++)
        {
            if (Drinks[i].ItemId == itemId) return i;
        }

        return null;
    }
}

/// <summary>
/// A stack of brewable food somewhere the player can reach, with what each unit of it gives as this stack holds it (a
/// part-used item gives less than the table says). Place is the name of where it is kept.
/// </summary>
public sealed record Stack(long InstanceId, int ItemId, int Count, long OwnerId, string Place, double? HoursLeft, double Sat, double Mood)
{
    public bool IsSpoiled => HoursLeft is <= 0;
}

/// <summary>0 idle, 1 brewing, 2 done: BrewComponent.State.</summary>
public enum BarrelState
{
    Idle = 0,
    Brewing = 1,
    Done = 2,
}

/// <summary>
/// A brewing barrel in the base: its state, the drink and bottles it is making or has made, the hours left and in all,
/// and what is in it now.
/// </summary>
public sealed record Barrel(long FurnitureId, string Name, BarrelState State, bool IsLocked, int ResultItemId, int ResultCount, int RemainHours, int TotalHours, IReadOnlyList<Stack> Contents);

/// <summary>
/// Everything one read of the game gives: the player (whose bag takes what comes out of a barrel), the rules, the
/// tables, the food in reach, every barrel, and the barrel whose screen is open with what the game says it holds.
/// CanSplit is true when Stacking can move a single unit off a stack.
/// </summary>
public sealed record Cellar(long RoleId, Rules Rules, Catalog Catalog, IReadOnlyList<Stack> Stock, IReadOnlyList<Barrel> Barrels, long? OpenId, Mix OpenMix, bool CanSplit)
{
    public Barrel? Open => OpenId is { } id ? BarrelOf(id) : null;

    public Barrel? BarrelOf(long furnitureId)
    {
        foreach (var barrel in Barrels)
        {
            if (barrel.FurnitureId == furnitureId) return barrel;
        }

        return null;
    }
}

/// <summary>What a read found: a game in progress, no game, or a game where brewing can't be read right now.</summary>
public abstract record BrewRead
{
    private BrewRead()
    {
    }

    public sealed record NoGame : BrewRead;

    public sealed record NotReady(string Reason) : BrewRead;

    public sealed record Ready(Cellar Cellar) : BrewRead;
}

/// <summary>What a press did, as one line for the player.</summary>
public sealed record Pressed(bool IsDone, string Message);
