using System;
using System.Collections.Generic;

namespace BrewingHub.Core;

/// <summary>
/// A long job run a slice at a time, so no single frame carries all of it. The job is a list of steps: a step that
/// yields true lets the next one run in the same slice, a step that yields false waits for the next slice (for work
/// running elsewhere to finish). Each slice runs at least one step, so the job always moves on.
/// </summary>
public sealed class SlicedJob
{
    private readonly IEnumerator<bool> _steps;

    public SlicedJob(IEnumerable<bool> steps)
    {
        _steps = steps.GetEnumerator();
    }

    public bool IsDone { get; private set; }

    /// <summary>Runs steps until the job ends, a step asks to wait, or isSliceSpent says the slice is used up. True once the job has ended.</summary>
    public bool Advance(Func<bool> isSliceSpent)
    {
        while (!IsDone)
        {
            if (!_steps.MoveNext())
            {
                IsDone = true;

                break;
            }

            if (!_steps.Current || isSliceSpent()) break;
        }

        return IsDone;
    }
}
