using System.Collections.Generic;

namespace BrewingHub.Core;

/// <summary>
/// Each stack as last read, by instance id. What a unit gives depends only on the item and how many uses it has left,
/// so while those stay the same the satiety and morale are not asked of the game again, and a stack where nothing at all
/// changed comes back as the same object. A read of everything sweeps the memo: stacks it did not meet are dropped.
/// </summary>
public sealed class StackMemo
{
    private Dictionary<long, Entry> _known = new();
    private Dictionary<long, Entry> _seen = new();
    private bool _isSweeping;

    /// <summary>Starts a read of everything; stacks it does not meet are gone at EndSweep.</summary>
    public void BeginSweep()
    {
        _seen.Clear();
        _isSweeping = true;
    }

    public void EndSweep()
    {
        if (!_isSweeping) return;

        (_known, _seen) = (_seen, _known);
        _seen.Clear();
        _isSweeping = false;
    }

    /// <summary>Drops everything, as when the game is left: instance ids mean nothing in another save.</summary>
    public void Forget()
    {
        _known.Clear();
        _seen.Clear();
        _isSweeping = false;
    }

    /// <summary>
    /// The stack as it reads now, when the item and its uses are as last kept: the same object when nothing changed, a
    /// copy with the new count, place or hours otherwise. Null when the satiety and morale must be read again.
    /// </summary>
    public Stack? Find(long instanceId, int configId, int useTimes, int maxUseTimes, int count, long ownerId, string place, double? hoursLeft)
    {
        if (!_known.TryGetValue(instanceId, out var entry) || entry.ConfigId != configId || entry.UseTimes != useTimes || entry.MaxUseTimes != maxUseTimes) return null;

        var stack = entry.Stack;

        if (stack.Count != count || stack.OwnerId != ownerId || stack.Place != place || stack.HoursLeft != hoursLeft)
        {
            stack = stack with { Count = count, OwnerId = ownerId, Place = place, HoursLeft = hoursLeft };
        }

        Remember(instanceId, entry with { Stack = stack });

        return stack;
    }

    /// <summary>Keeps a stack read in full, with the item and uses its satiety and morale came from.</summary>
    public Stack Keep(int configId, int useTimes, int maxUseTimes, Stack stack)
    {
        Remember(stack.InstanceId, new Entry(configId, useTimes, maxUseTimes, stack));

        return stack;
    }

    private void Remember(long instanceId, Entry entry)
    {
        _known[instanceId] = entry;

        if (_isSweeping) _seen[instanceId] = entry;
    }

    private readonly record struct Entry(int ConfigId, int UseTimes, int MaxUseTimes, Stack Stack);
}
