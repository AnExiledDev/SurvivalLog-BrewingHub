using System;
using System.Collections.Generic;

namespace BrewingHub.Core;

/// <summary>
/// A fingerprint of everything BrewView.State reads, so an unchanged read is spotted without building the JSON. Two
/// reads with the same print draw the same page; the catalog is checked apart, by reference, since it is built once per
/// game and language. Only the open barrel's contents count, since the state shows no other barrel's.
/// </summary>
public static class StatePrint
{
    public static ulong Of(Cellar cellar, BrewExtras extras)
    {
        var print = new Print();
        var rules = cellar.Rules;

        print.Add(rules.Low);
        print.Add(rules.High);
        print.Add(rules.PerBottle);
        print.Add(rules.MaxBottles);
        print.Add(rules.MinSatiety);
        print.Add(rules.HourBase);
        print.Add(rules.HourPerSatiety);
        print.Add(rules.HourMax);
        print.Add(rules.SpeedCut);
        print.Add(cellar.CanSplit ? 1 : 0);
        print.Add(extras.IsSpoilingFirst ? 1 : 0);
        print.Add(cellar.OpenId ?? long.MinValue);
        print.Add(cellar.OpenMix.Sat);
        print.Add(cellar.OpenMix.Mood);
        print.Add(cellar.OpenMix.Count);
        AddStacks(ref print, cellar.Stock);
        print.Add(cellar.Barrels.Count);

        for (var i = 0; i < cellar.Barrels.Count; i++)
        {
            var barrel = cellar.Barrels[i];
            var isOpen = barrel.FurnitureId == cellar.OpenId;

            print.Add(barrel.FurnitureId);
            print.Add(barrel.Name);
            print.Add((int)barrel.State);
            print.Add(barrel.IsLocked ? 1 : 0);
            print.Add(barrel.ResultItemId);
            print.Add(barrel.ResultCount);
            print.Add(barrel.RemainHours);
            print.Add(barrel.TotalHours);
            print.Add(extras.Again.Contains(barrel.FurnitureId) ? 1 : 0);
            print.Add(isOpen ? 1 : 0);

            if (isOpen) AddStacks(ref print, barrel.Contents);
        }

        return print.Value;
    }

    /// <summary>
    /// What the bag and the open barrel hold: the food in reach of the game's own brewing screen. When it changes
    /// without a press here, food was moved by hand, so the storages may have changed too.
    /// </summary>
    public static ulong OfHeld(IReadOnlyList<Stack> stock, long roleId, Barrel? open)
    {
        var print = new Print();

        for (var i = 0; i < stock.Count; i++)
        {
            var stack = stock[i];

            if (stack.OwnerId != roleId) continue;

            print.Add(stack.InstanceId);
            print.Add(stack.Count);
        }

        print.Add(open?.FurnitureId ?? long.MinValue);

        for (var i = 0; open != null && i < open.Contents.Count; i++)
        {
            print.Add(open.Contents[i].InstanceId);
            print.Add(open.Contents[i].Count);
        }

        return print.Value;
    }

    private static void AddStacks(ref Print print, IReadOnlyList<Stack> stacks)
    {
        print.Add(stacks.Count);

        for (var i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];

            print.Add(stack.InstanceId);
            print.Add(stack.ItemId);
            print.Add(stack.Count);
            print.Add(stack.OwnerId);
            print.Add(stack.Place);
            print.Add(stack.HoursLeft);
            print.Add(stack.Sat);
            print.Add(stack.Mood);
        }
    }

    /// <summary>A running 64-bit hash: each value is folded in and mixed, so order matters and near values spread apart.</summary>
    private struct Print
    {
        public ulong Value;

        public void Add(long value) => Value = Mix(Value * 31 + (ulong)value + 0x9E3779B97F4A7C15UL);

        public void Add(string? value) => Add(value == null ? -1 : StringComparer.Ordinal.GetHashCode(value));

        public void Add(double? value) => Add(value is { } number ? BitConverter.DoubleToInt64Bits(number) : long.MinValue);

        private static ulong Mix(ulong x)
        {
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;

            return x ^ (x >> 31);
        }
    }
}
