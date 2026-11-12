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
/// Picks the barrel for one drink from the food in reach. The game uses up everything in the barrel and pays only in
/// whole bottles, and the drink is the same anywhere inside its band (Furniture.TryFinishBrew never reads the quality),
/// so the best barrel is the one with the most bottles, the least satiety that fills no bottle, the fewest lines (a food
/// from one place) and the fewest items. It tries every food on its own, every pair of foods, and as a last resort a
/// fill that mixes as many foods as it needs. On a tie the food closest to spoiling wins (or the freshest), and one
/// food's stacks are taken in that order too. Spoiled food is never used.
/// </summary>
public static class Planner
{
    // The score, in bottles: a bottle is worth two, then the satiety that fills no bottle, a line and an item cost less.
    private const double BottleWorth = 2;
    private const double LineCost = 1;
    private const double ItemCost = 0.1;
    private const double SameScore = 1e-9;
    private const double NeverSpoils = 1e6;

    // The mixing fill used as a last resort.
    private const int MaxSteps = 400;
    private const double MostOvershoot = 0.6;

    public static Plan? For(Rules rules, IEnumerable<Stack> pool, int tier, bool isSpoilingFirst, bool canSplit)
    {
        var order = InOrder(pool.Where(IsUsable), isSpoilingFirst).ToList();

        if (order.Count == 0) return null;

        var kinds = KindsOf(rules, order, canSplit);
        var best = new Best(rules, tier, isSpoilingFirst, canSplit);

        foreach (var kind in kinds)
        {
            for (var n = 1; n <= kind.Most; n++) best.Try(kind, n, null, 0);
        }

        foreach (var a in kinds)
        {
            foreach (var b in kinds)
            {
                if (a != b) TryPairs(rules, tier, best, a, b);
            }
        }

        best.Try(Fill(rules, order, tier, canSplit));
        best.Try(Fill(rules, order.OrderByDescending(stack => stack.Sat).ToList(), tier, canSplit));

        return best.Plan();
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

    private static bool IsUsable(Stack stack) => !stack.IsSpoiled && stack.Sat > 0 && stack.Count > 0;

    /// <summary>
    /// For i of food a, the least of food b that gives the most bottles the two can make, or one bottle fewer. When the
    /// mix misses the drink, more of b moves the quality toward b's own, so it keeps adding b while that can still reach
    /// the drink.
    /// </summary>
    private static void TryPairs(Rules rules, int tier, Best best, Kind a, Kind b)
    {
        for (var i = 1; i <= a.Most; i++)
        {
            var satA = a.Sat * a.Units[i];
            var top = Math.Min(rules.MaxBottles, (int)((satA + b.Sat * b.Units[b.Most]) / rules.PerBottle));

            for (var bottles = top; bottles >= Math.Max(1, top - 1); bottles--)
            {
                var need = Math.Max(1, (int)Math.Ceiling((bottles * rules.PerBottle - satA) / b.Sat - 1e-9));

                for (var j = need; j <= b.Most; j++)
                {
                    var mixTier = BrewMath.TierOf(rules, Mix.None.Add(a.Sat, a.Mood, a.Units[i]).Add(b.Sat, b.Mood, b.Units[j]));

                    if (mixTier == tier)
                    {
                        best.Try(a, i, b, j);

                        break;
                    }

                    if ((mixTier < tier && b.Tier < tier) || (mixTier > tier && b.Tier > tier)) break;
                }
            }
        }
    }

    /// <summary>The usable stacks grouped by food: same item, same satiety and morale (a part-used item is its own food).</summary>
    private static List<Kind> KindsOf(Rules rules, List<Stack> order, bool canSplit)
    {
        var kinds = new List<Kind>();

        foreach (var group in order.GroupBy(stack => (stack.ItemId, stack.Sat, stack.Mood)))
        {
            kinds.Add(new Kind(rules, group.ToList(), canSplit));
        }

        return kinds;
    }

    /// <summary>
    /// A greedy fill for food too scarce for one or two foods to fill the barrel: a chunk at a time in the given order,
    /// keeping the mix inside the drink's band, until the barrel is full, then without the food that adds no bottle.
    /// Null when it took nothing; the caller still checks the drink.
    /// </summary>
    private static List<Take>? Fill(Rules rules, List<Stack> order, int tier, bool canSplit)
    {
        var left = new int[order.Count];
        var taken = new int[order.Count];
        var mix = Mix.None;

        for (var i = 0; i < order.Count; i++) left[i] = order[i].Count;

        for (var step = 0; step < MaxSteps && mix.Sat < rules.Capacity; step++)
        {
            var pick = FirstFitting(rules, mix, order, left, tier, canSplit, isFirstBottle: false);

            if (pick < 0) pick = FirstFitting(rules, mix, order, left, tier, canSplit, isFirstBottle: true);

            if (pick < 0) break;

            var chunk = Chunk(order[pick], left[pick], canSplit);

            mix = mix.Add(order[pick], chunk);
            left[pick] -= chunk;
            taken[pick] += chunk;
        }

        var takes = new List<Take>();

        for (var i = 0; i < order.Count; i++)
        {
            if (taken[i] > 0) takes.Add(new Take(order[i], taken[i]));
        }

        return takes.Count > 0 ? TrimSurplus(rules, takes, canSplit) : null;
    }

    /// <summary>
    /// The takes without the food that adds no bottle: last take first, every chunk the barrel can lose and still brew
    /// the same drink and bottles comes back out.
    /// </summary>
    private static List<Take> TrimSurplus(Rules rules, List<Take> takes, bool canSplit)
    {
        var units = new int[takes.Count];
        var mix = Mix.None;

        for (var i = 0; i < takes.Count; i++)
        {
            units[i] = takes[i].Units;
            mix = mix.Add(takes[i].Stack, units[i]);
        }

        var brew = BrewMath.Of(rules, mix);

        for (var i = takes.Count - 1; i >= 0; i--)
        {
            while (units[i] > 0)
            {
                var chunk = canSplit ? 1 : units[i];
                var next = BrewMath.Of(rules, mix.Add(takes[i].Stack, -chunk));

                if (!next.CanStart || next.Tier != brew.Tier || next.Bottles != brew.Bottles) break;

                mix = mix.Add(takes[i].Stack, -chunk);
                units[i] -= chunk;
            }
        }

        var trimmed = new List<Take>();

        for (var i = 0; i < takes.Count; i++)
        {
            if (units[i] > 0) trimmed.Add(units[i] == takes[i].Units ? takes[i] : takes[i] with { Units = units[i] });
        }

        return trimmed;
    }

    /// <summary>The first stack in order with some left whose next chunk fits, or -1.</summary>
    private static int FirstFitting(Rules rules, Mix mix, List<Stack> order, int[] left, int tier, bool canSplit, bool isFirstBottle)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (left[i] > 0 && Fits(rules, mix, order[i], Chunk(order[i], left[i], canSplit), tier, isFirstBottle)) return i;
        }

        return -1;
    }

    /// <summary>
    /// A chunk fits when it keeps the barrel within a little over its last bottle and the mix inside the band. Food that
    /// keeps the band is always tried first; only when none does may other food go in, and only while the barrel stays
    /// under one bottle, since two foods outside the band can mix to one inside it.
    /// </summary>
    private static bool Fits(Rules rules, Mix mix, Stack stack, int chunk, int tier, bool isFirstBottle)
    {
        var next = mix.Add(stack, chunk);

        if (next.Sat - rules.Capacity > rules.PerBottle * MostOvershoot) return false;

        return isFirstBottle ? next.Sat < rules.PerBottle : BrewMath.TierOf(rules, next) == tier;
    }

    /// <summary>Hours left, with food that never spoils counted as very fresh.</summary>
    private static double HoursOf(Stack stack) => stack.HoursLeft ?? NeverSpoils;

    /// <summary>
    /// One food's stacks, in freshness order, and for each count n asked of it the units that really go in (more than n
    /// when whole stacks must go in) and the lines they take.
    /// </summary>
    private sealed class Kind
    {
        private readonly List<Place> _places = new();
        private readonly List<Place> _bySize;
        private readonly bool _canSplit;

        public Kind(Rules rules, List<Stack> stacks, bool canSplit)
        {
            _canSplit = canSplit;
            Sat = stacks[0].Sat;
            Mood = stacks[0].Mood;
            Tier = BrewMath.TierOf(rules, Mood / Sat);
            Hours = HoursOf(stacks[0]);

            foreach (var stack in stacks)
            {
                var place = _places.Find(p => p.Name == stack.Place);

                if (place == null) _places.Add(place = new Place(stack.Place));

                place.Stacks.Add(stack);
                place.Units += stack.Count;
            }

            _bySize = _places.OrderByDescending(place => place.Units).ToList();

            var total = stacks.Sum(stack => stack.Count);

            Most = Math.Max(1, Math.Min(total, (int)Math.Ceiling(rules.Capacity / Sat - 1e-9)));
            Units = new int[Most + 1];
            Lines = new int[Most + 1];

            for (var n = 1; n <= Most; n++) (Units[n], Lines[n]) = Walk(n, null);
        }

        public double Sat { get; }

        public double Mood { get; }

        /// <summary>The drink this food makes on its own.</summary>
        public int Tier { get; }

        /// <summary>Hours left on its stack closest to spoiling (or freshest, in the order asked).</summary>
        public double Hours { get; }

        public int Most { get; }

        public int[] Units { get; }

        public int[] Lines { get; }

        /// <summary>
        /// The takes for n units: all from the first place that holds n. Else place by place in freshness order, unless
        /// that needs more places than taking the biggest first. Stacks within a place go in freshness order.
        /// </summary>
        public (int Units, int Lines) Walk(int n, List<Take>? into)
        {
            var home = _places.Find(place => place.Units >= n);

            if (home != null) return (TakeFrom(home, n, 0, into), 1);

            var route = PlacesFor(_places, n) <= PlacesFor(_bySize, n) ? _places : _bySize;
            var got = 0;
            var lines = 0;

            foreach (var place in route)
            {
                if (got >= n) break;

                got = TakeFrom(place, n, got, into);
                lines++;
            }

            return (got, lines);
        }

        /// <summary>How many places, taken in this order, it takes to hold n units.</summary>
        private static int PlacesFor(List<Place> route, int n)
        {
            var units = 0;
            var places = 0;

            foreach (var place in route)
            {
                if (units >= n) break;

                units += place.Units;
                places++;
            }

            return places;
        }

        /// <summary>Takes from one place's stacks until n units are in, and returns the units in so far.</summary>
        private int TakeFrom(Place place, int n, int got, List<Take>? into)
        {
            foreach (var stack in place.Stacks)
            {
                if (got >= n) break;

                var units = _canSplit ? Math.Min(stack.Count, n - got) : stack.Count;

                got += units;
                into?.Add(new Take(stack, units));
            }

            return got;
        }
    }

    private sealed class Place
    {
        public Place(string name) => Name = name;

        public string Name { get; }

        public List<Stack> Stacks { get; } = new();

        public int Units { get; set; }
    }

    /// <summary>i of food A, and j of food B when there is one.</summary>
    private sealed record Picked(Kind A, int I, Kind? B, int J)
    {
        public List<Take> Takes()
        {
            var takes = new List<Take>();

            A.Walk(I, takes);
            B?.Walk(J, takes);

            return takes;
        }
    }

    /// <summary>The best barrel tried so far, kept as what to rebuild it from.</summary>
    private sealed class Best
    {
        private readonly Rules _rules;
        private readonly int _tier;
        private readonly bool _isSpoilingFirst;
        private readonly bool _canSplit;
        private double _score = double.NegativeInfinity;
        private double _hours;
        private Picked? _picked;
        private List<Take>? _takes;

        public Best(Rules rules, int tier, bool isSpoilingFirst, bool canSplit)
        {
            _rules = rules;
            _tier = tier;
            _isSpoilingFirst = isSpoilingFirst;
            _canSplit = canSplit;
        }

        public void Try(Kind a, int i, Kind? b, int j)
        {
            var unitsA = a.Units[i];
            var unitsB = b == null ? 0 : b.Units[j];
            var mix = Mix.None.Add(a.Sat, a.Mood, unitsA);

            if (b != null) mix = mix.Add(b.Sat, b.Mood, unitsB);

            var hours = (a.Hours * unitsA + (b == null ? 0 : b.Hours * unitsB)) / (unitsA + unitsB);

            if (!Keep(mix, a.Lines[i] + (b == null ? 0 : b.Lines[j]), hours)) return;

            _picked = new Picked(a, i, b, j);
            _takes = null;
        }

        public void Try(List<Take>? takes)
        {
            if (takes == null) return;

            var mix = Mix.None;
            var hours = 0.0;
            var lines = new HashSet<(int, string)>();

            foreach (var take in takes)
            {
                mix = mix.Add(take.Stack, take.Units);
                hours += HoursOf(take.Stack) * take.Units;
                lines.Add((take.Stack.ItemId, take.Stack.Place));
            }

            if (!Keep(mix, lines.Count, hours / mix.Count)) return;

            _picked = null;
            _takes = takes;
        }

        public Plan? Plan()
        {
            var picked = _takes ?? _picked?.Takes();

            if (picked == null) return null;

            var takes = TrimSurplus(_rules, picked, _canSplit);
            var mix = Mix.None;

            foreach (var take in takes) mix = mix.Add(take.Stack, take.Units);

            return new Plan(_tier, takes, BrewMath.Of(_rules, mix));
        }

        /// <summary>Whether this barrel makes the drink and beats the best so far; when it does, it becomes the best.</summary>
        private bool Keep(Mix mix, int lines, double hours)
        {
            var brew = BrewMath.Of(_rules, mix);

            if (!brew.CanStart || brew.Tier != _tier) return false;

            var score = BottleWorth * brew.Bottles - brew.Wasted / _rules.PerBottle - LineCost * lines - ItemCost * mix.Count;
            var isBetter = score > _score + SameScore
                || (score >= _score - SameScore && (_isSpoilingFirst ? hours < _hours : hours > _hours));

            if (!isBetter) return false;

            _score = score;
            _hours = hours;

            return true;
        }
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
