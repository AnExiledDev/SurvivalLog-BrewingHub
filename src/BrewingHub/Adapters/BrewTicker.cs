using System;
using System.Collections.Generic;
using ExiledToolkit.Kit;
using UnityEngine;

namespace BrewingHub.Adapters;

/// <summary>
/// The Unity object that hands frames to the session. Each distinct failure is logged once, never rethrown into the
/// game; a failure that repeats every frame would otherwise flood the log.
/// </summary>
public sealed class BrewTicker : MonoBehaviour
{
    private const int MaxReportedFailures = 20;

    internal static BrewSession? Session;
    internal static Action<string> Log = _ => { };
    private static readonly HashSet<string> ReportedFailures = new();

    /// <summary>The Toolkit's clock at the mod's first read of it; the mod's timers count from there.</summary>
    private static double _startedAt = double.NaN;

    public BrewTicker(IntPtr pointer) : base(pointer)
    {
    }

    /// <summary>
    /// Seconds since the mod first asked, for its timers. The Toolkit's clock counts from system start, and the
    /// session's timers are floats that start at 0, so they count from the mod's first read instead. Main thread only.
    /// </summary>
    internal static float Now()
    {
        var now = Clock.Now;

        if (double.IsNaN(_startedAt)) _startedAt = now;

        return (float)(now - _startedAt);
    }

    private void Update()
    {
        try
        {
            Session?.Update();
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    private static void ReportFailure(Exception ex)
    {
        if (ReportedFailures.Count >= MaxReportedFailures || !ReportedFailures.Add(ex.GetType().FullName + ": " + ex.Message)) return;

        Log("update failed (each distinct failure reported once): " + ex);

        if (ReportedFailures.Count == MaxReportedFailures) Log($"{MaxReportedFailures} distinct update failures reported; further ones are not logged");
    }
}
