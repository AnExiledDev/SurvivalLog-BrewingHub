using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using BrewingHub.Core;
using HarmonyLib;
using Vuplex.WebView.Internal;

namespace BrewingHub.Adapters;

/// <summary>
/// Catches the brewing screen's Brewing Hub messages before the game's web layer sees them (it would log them as
/// unknown) and queues them for the next frame, so nothing runs on the web view's thread. Static because a Harmony
/// prefix has nowhere else to look.
/// </summary>
internal static class PageMessages
{
    private static readonly ConcurrentQueue<PageCommand> Queue = new();
    private static Action<string> _log = _ => { };
    private static int _hasReportedFailure;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted"), prefix: new HarmonyMethod(typeof(PageMessages), nameof(TakeOurs)));
            log("listening for brewing screen messages");
        }
        catch (Exception ex)
        {
            log("could not hook brewing screen messages, Brewing Hub's buttons will do nothing: " + ex.Message);
        }
    }

    public static bool TryTake([NotNullWhen(true)] out PageCommand? command) => Queue.TryDequeue(out command);

    /// <summary>Never throws into the web view: on any failure the message goes on to the game as if the mod were not here.</summary>
    private static bool TakeOurs(string serializedMessage)
    {
        try
        {
            if (!PageCommand.IsOurs(serializedMessage)) return true;

            if (PageCommand.Parse(serializedMessage) is { } command) Queue.Enqueue(command);

            return false;
        }
        catch (Exception ex)
        {
            ReportOnce(ex);

            return true;
        }
    }

    /// <summary>Runs on the web view's thread, so the once-only flag is set atomically.</summary>
    private static void ReportOnce(Exception ex)
    {
        if (Interlocked.Exchange(ref _hasReportedFailure, 1) == 0) _log("reading a web UI message failed, it was passed on to the game (reported once): " + ex);
    }
}
