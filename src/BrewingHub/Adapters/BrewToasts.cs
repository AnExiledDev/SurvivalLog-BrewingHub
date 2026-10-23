using System;
using System.IO;
using System.Runtime.CompilerServices;
using BrewingHub.Core;

namespace BrewingHub.Adapters;

/// <summary>
/// Shows a barrel notice as a toast through Exiled's Toolkit when it is installed. Show returns false when no toast
/// could be shown, so the caller can fall back to the page's own line. Only <see cref="ShowWithToolkit"/> names a
/// Toolkit type, so the mod loads without it.
/// </summary>
internal sealed class BrewToasts
{
    private readonly Action<string> _log;
    private bool _isBroken;
    private bool _hasReportedFailure;

    public BrewToasts(Action<string> log) => _log = log;

    public bool Show(string text, bool isWarning, bool isChinese)
    {
        if (_isBroken || !OptionalLive.IsToolkitInstalled) return false;

        try
        {
            ShowWithToolkit(text, isWarning, isChinese);

            return true;
        }
        catch (Exception ex) when (ex is TypeLoadException or MissingMethodException or FileNotFoundException or FileLoadException)
        {
            _isBroken = true;
            ReportOnce($"Exiled's Toolkit could not show a toast, so barrel notices are shown on the brewing screen only: {ex.Message}");
        }
        catch (Exception ex)
        {
            ReportOnce($"could not show a barrel toast (reported once): {ex.Message}");
        }

        return false;
    }

    // Kept in its own method so the Toolkit's types are only looked up when it is installed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ShowWithToolkit(string text, bool isWarning, bool isChinese) =>
        ExiledToolkit.Toasts.Show(new ExiledToolkit.ToastRequest(Texts.ToastSource.Pick(isChinese), text)
        {
            Kind = isWarning ? ExiledToolkit.ToastKind.Warning : ExiledToolkit.ToastKind.Success,
        });

    private void ReportOnce(string message)
    {
        if (_hasReportedFailure) return;

        _hasReportedFailure = true;
        _log(message);
    }
}
