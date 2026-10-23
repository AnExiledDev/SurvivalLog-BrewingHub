using System;
using System.Collections.Generic;
using System.Linq;

namespace BrewingHub.Core;

/// <summary>Some units of one stack going into a barrel.</summary>
public sealed record Take(Stack Stack, int Units);

/// <summary>A full barrel of one drink: what goes in, from where, and what it brews.</summary>
public sealed record Plan(int Tier, IReadOnlyList<Take> Takes, Brew Brew)
{
    public int Units => Takes.Sum(take => take.Units);
}

/// <summary>
/// Fills a barrel for one drink from the food in reach. Greedy: food closest to spoiling first (or freshest first), a
/// unit at a time when Stacking can split stacks, else a whole stack at a time, keeping the mix's quality inside the
/// drink's band, until the barrel holds its last bottle's worth. Spoiled food is never used.
/// </summary>
public static class Planner
{
    private const int MaxSteps = 400;
    private const double MostOvershoot = 0.6;

    public static Plan? For(Rules rules, IEnumerable<Stack> pool, int tier, bool isSpoilingFirst, bool canSplit)
    {
        var order = InOrder(pool.Where(stack => !stack.IsSpoiled && stack.Sat > 0 && stack.Count > 0), isSpoilingFirst).ToList();
        var left = order.ToDictionary(stack => stack, stack => stack.Count);
        var units = new Dictionary<Stack, int>();
        var mix = Mix.None;

        for (var step = 0; step < MaxSteps && mix.Sat < rules.Capacity; step++)
        {
            var pick = order.FirstOrDefault(stack => left[stack] > 0 && Fits(rules, mix, stack, Chunk(stack, left[stack], canSplit), tier, isFirstBottle: false))
                ?? order.FirstOrDefault(stack => left[stack] > 0 && Fits(rules, mix, stack, Chunk(stack, left[stack], canSplit), tier, isFirstBottle: true));

            if (pick == null) break;

            var chunk = Chunk(pick, left[pick], canSplit);

            mix = mix.Add(pick, chunk);
            left[pick] -= chunk;
            units[pick] = units.TryGetValue(pick, out var taken) ? taken + chunk : chunk;
        }

        var brew = BrewMath.Of(rules, mix);

        if (!brew.CanStart || brew.Tier != tier) return null;

        return new Plan(tier, order.Where(units.ContainsKey).Select(stack => new Take(stack, units[stack])).ToList(), brew);
    }

    /// <summary>Food closest to spoiling first, or the freshest first; food that never spoils counts as freshest. Ties go to the most filling.</summary>
    public static IEnumerable<Stack> InOrder(IEnumerable<Stack> stacks, bool isSpoilingFirst)
    {
        var byFreshness = isSpoilingFirst
            ? stacks.OrderBy(stack => stack.HoursLeft ?? double.MaxValue)
            : stacks.OrderByDescending(stack => stack.HoursLeft ?? double.MaxValue);

        return byFreshness.ThenByDescending(stack => stack.Sat);
    }

    /// <summary>One unit when Stacking can split the stack, else everything left of it.</summary>
    public static int Chunk(Stack stack, int left, bool canSplit) => canSplit ? 1 : left;

    /// <summary>
    /// A chunk fits when it keeps the barrel within a little over its last bottle and the mix inside the band. Food that
    /// keeps the band is always tried first; only when none does may other food go in, and only while the barrel stays
    /// under one bottle, since two foods outside the band can mix to one inside it. Taking out-of-band food first would
    /// shut out food that would have made the drink.
    /// </summary>
    private static bool Fits(Rules rules, Mix mix, Stack stack, int chunk, int tier, bool isFirstBottle)
    {
        var next = BrewMath.Of(rules, mix.Add(stack, chunk));

        if (next.Sat - rules.Capacity > rules.PerBottle * MostOvershoot) return false;

        return isFirstBottle ? next.Sat < rules.PerBottle : next.Tier == tier;
    }
}

public enum NudgeKind
{
    Add,
    Remove,
}

/// <summary>A one-step change to the open barrel worth showing: put in some of a stack, or take some out.</summary>
public sealed record Nudge(NudgeKind Kind, Stack Stack, int Units, Brew Next, double Score);

/// <summary>
/// The changes to a barrel being filled that make a better drink: a better tier first, then one more bottle of the same
/// drink. At most three, one per item and kind.
/// </summary>
public static class Nudges
{
    private const int MaxShown = 3;

    public static IReadOnlyList<Nudge> For(Rules rules, Mix mix, IEnumerable<Stack> contents, IEnumerable<Stack> stock, bool isSpoilingFirst, bool canSplit)
    {
        if (mix.Count <= 0) return Array.Empty<Nudge>();

        var now = BrewMath.Of(rules, mix);
        var found = new List<Nudge>();

        foreach (var stack in Planner.InOrder(stock.Where(stack => !stack.IsSpoiled && stack.Sat > 0 && stack.Count > 0), isSpoilingFirst))
        {
            var units = Planner.Chunk(stack, stack.Count, canSplit);
            var next = BrewMath.Of(rules, mix.Add(stack, units));

            if (next.Tier > now.Tier) found.Add(new Nudge(NudgeKind.Add, stack, units, next, 100 + next.Tier));
            else if (next.Tier == now.Tier && next.Bottles > now.Bottles) found.Add(new Nudge(NudgeKind.Add, stack, units, next, 50 + next.Bottles - next.Wasted / 100));
        }

        foreach (var stack in contents.Where(stack => stack.Count > 0))
        {
            var units = Planner.Chunk(stack, stack.Count, canSplit);
            var next = BrewMath.Of(rules, mix.Add(stack, -units));

            if (next.CanStart && next.Tier > now.Tier) found.Add(new Nudge(NudgeKind.Remove, stack, units, next, 90 + next.Tier));
        }

        return found
            .OrderByDescending(nudge => nudge.Score)
            .GroupBy(nudge => (nudge.Kind, nudge.Stack.ItemId))
            .Select(group => group.First())
            .Take(MaxShown)
            .ToList();
    }
}
