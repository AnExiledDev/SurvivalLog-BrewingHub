using System;
using System.Reflection;

namespace BrewingHub.Adapters;

/// <summary>
/// Stacking's one-unit move, found by name so Brewing Hub runs without Stacking installed. With Stacking, a fill can move
/// a single unit off a stack, one at a time, and learn at once whether each went in. Null means
/// Stacking is absent or skipped the move (a single item), and the caller moves the stack itself.
/// </summary>
internal static class StackingUnits
{
    private static readonly Lazy<Func<long, long, long, bool?>?> Mover = new(Find);

    public static bool IsPresent => Mover.Value != null;

    public static bool? MoveOne(long itemId, long fromOwnerId, long toOwnerId) => Mover.Value?.Invoke(itemId, fromOwnerId, toOwnerId);

    private static Func<long, long, long, bool?>? Find()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var method = assembly.GetType("Stacking.UnitMoves", false)?.GetMethod("MoveOne", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(long), typeof(long), typeof(long) }, null);

            if (method != null && method.ReturnType == typeof(bool?)) return (Func<long, long, long, bool?>)Delegate.CreateDelegate(typeof(Func<long, long, long, bool?>), method);
        }

        return null;
    }
}
