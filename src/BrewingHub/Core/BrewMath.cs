using System;

namespace BrewingHub.Core;

/// <summary>A barrel's contents reduced to what the game adds up: satiety, morale and the number of items.</summary>
public readonly record struct Mix(double Sat, double Mood, int Count)
{
    public static readonly Mix None = new(0, 0, 0);

    /// <summary>n more units that each give this satiety and morale (n may be negative to take some out).</summary>
    public Mix Add(double sat, double mood, int n) => new(Sat + sat * n, Mood + mood * n, Count + n);

    public Mix Add(Stack stack, int n) => Add(stack.Sat, stack.Mood, n);
}

/// <summary>What a mix brews: quality (morale over satiety), the drink tier, bottles, hours, and the satiety that fills no bottle.</summary>
public sealed record Brew(double Sat, double Mood, double Quality, int Tier, int Bottles, bool CanStart, int Hours, double Wasted, double NextBottle, int Count);

/// <summary>
/// The barrel's arithmetic as Furniture.CalcBrewPreview does it (build 1.1.19611): quality is total morale over total
/// satiety; below the first line is Poor, below the second Average, else Strong; bottles are whole satiety-per-bottle
/// lots, at least 1 and at most the cap; hours are a base plus more per satiety, capped, cut by the speed talent, and
/// rounded the way Unity's Mathf.RoundToInt does (halves to even).
/// </summary>
public static class BrewMath
{
    private const double SlowestSpeedFactor = 0.1;

    public static int TierOf(Rules rules, double quality)
    {
        if (quality < rules.Low) return 0;

        return quality < rules.High ? 1 : 2;
    }

    public static Brew Of(Rules rules, Mix mix)
    {
        var quality = mix.Sat > 0 ? mix.Mood / mix.Sat : 0;
        var bottles = Math.Min(rules.MaxBottles, Math.Max(1, (int)(mix.Sat / rules.PerBottle)));
        var canStart = mix.Count > 0 && mix.Sat >= rules.MinSatiety;
        var used = bottles * rules.PerBottle;
        var nextBottle = bottles < rules.MaxBottles ? used + rules.PerBottle - mix.Sat : 0;

        return new Brew(
            mix.Sat,
            mix.Mood,
            quality,
            TierOf(rules, quality),
            mix.Count > 0 ? bottles : 0,
            canStart,
            Hours(rules, mix.Sat),
            canStart ? Math.Max(0, mix.Sat - used) : 0,
            nextBottle,
            mix.Count);
    }

    public static int Hours(Rules rules, double sat)
    {
        var hours = Math.Min(Math.Max(rules.HourBase + rules.HourPerSatiety * sat, rules.HourBase), rules.HourMax);

        if (rules.SpeedCut > 0) hours *= Math.Max(SlowestSpeedFactor, 1 - rules.SpeedCut);

        return (int)Math.Round(Math.Max(1, hours), MidpointRounding.ToEven);
    }

    /// <summary>How many items of this kind it takes to fill one bottle on their own.</summary>
    public static int PerBottle(Rules rules, double sat) => sat > 0 ? Math.Max(1, (int)Math.Ceiling(rules.PerBottle / sat - 1e-9)) : 0;

    /// <summary>How many more of an item lift the mix to the tier, up to 20; null when 20 do not.</summary>
    public static int? ToReach(Rules rules, Mix mix, double sat, double mood, int tier)
    {
        for (var n = 1; n <= 20; n++)
        {
            if (Of(rules, mix.Add(sat, mood, n)).Tier >= tier) return n;
        }

        return null;
    }

    /// <summary>For a Strong mix: how many of an item can go in and keep it Strong, up to the cap of 9.</summary>
    public static int KeepsTop(Rules rules, Mix mix, double sat, double mood)
    {
        var n = 0;

        while (n < 9 && Of(rules, mix.Add(sat, mood, n + 1)).Tier == 2) n++;

        return n;
    }
}
