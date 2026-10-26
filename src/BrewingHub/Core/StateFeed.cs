using System;

namespace BrewingHub.Core;

/// <summary>
/// Sends the page the catalog and the state, each only when it changed. A read is fingerprinted first, so an unchanged
/// one costs no JSON at all. The catalog goes before the state that names its ids; when it does not get through, the
/// state waits for the next read, so both are tried again.
/// </summary>
public sealed class StateFeed
{
    private readonly Func<string, bool> _sendCatalog;
    private readonly Func<string, bool> _sendState;
    private Catalog? _sentCatalog;
    private ulong? _shownPrint;
    private bool _isHiddenShown;

    public StateFeed(Func<string, bool> sendCatalog, Func<string, bool> sendState)
    {
        _sendCatalog = sendCatalog;
        _sendState = sendState;
    }

    /// <summary>The state of an open brewing screen, or none (a null cellar) when it is closed.</summary>
    public void Send(Cellar? cellar, BrewExtras extras)
    {
        if (cellar == null)
        {
            SendHidden();

            return;
        }

        var print = StatePrint.Of(cellar, extras);

        if (print == _shownPrint && ReferenceEquals(cellar.Catalog, _sentCatalog)) return;

        if (!ReferenceEquals(cellar.Catalog, _sentCatalog))
        {
            if (!_sendCatalog(BrewView.Catalog(cellar.Catalog))) return;

            _sentCatalog = cellar.Catalog;
        }

        if (!_sendState(BrewView.State(cellar, extras))) return;

        _shownPrint = print;
        _isHiddenShown = false;
    }

    /// <summary>Sends the state again on the next read, as after a settings change.</summary>
    public void Forget()
    {
        _shownPrint = null;
        _isHiddenShown = false;
    }

    /// <summary>Sends the catalog and the state again on the next read, as when the page has just loaded.</summary>
    public void ForgetCatalog()
    {
        _sentCatalog = null;
        Forget();
    }

    private void SendHidden()
    {
        if (_isHiddenShown) return;

        if (!_sendState(BrewView.NoState())) return;

        _isHiddenShown = true;
        _shownPrint = null;
    }
}
