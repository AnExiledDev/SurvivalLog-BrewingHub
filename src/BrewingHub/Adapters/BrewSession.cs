using System;
using System.Collections.Generic;
using BrewingHub.Core;
using UnityEngine;

namespace BrewingHub.Adapters;

/// <summary>
/// Brewing Hub's game side, run once a frame: the catalog and state page.js draws on the game's own brewing screen, the
/// presses the page asks for, and what happens when a barrel finishes or is collected. Page messages arrive on the web
/// view's thread, so they are queued and handled here, in this frame.
/// </summary>
internal sealed class BrewSession
{
    private const float OpenRefreshSeconds = 0.5f;
    private const float ClosedRefreshSeconds = 5f;

    private readonly IBrewGame _game;
    private readonly WebPage _page;
    private readonly Func<string, bool, bool> _toast;
    private readonly Func<int, (string Place, int Bottles)?> _putAway;
    private readonly Func<bool> _isBrewScreenOpen;
    private readonly WebKeyboard _keyboard = new();
    private readonly Action<string> _log;

    private bool _isSpoilingFirst = true;
    private bool _isPuttingAway;

    private string _shownState = "";
    private Catalog? _sentCatalog;
    private Catalog? _catalog;
    private float _nextRefreshAt;
    private bool _wasScreenOpen;

    /// <summary>Barrels that brew again when collected, the drink each last brewed, and each barrel as last seen; main thread only.</summary>
    private readonly HashSet<long> _again = new();
    private readonly Dictionary<long, int> _lastTier = new();
    private readonly Dictionary<long, Barrel> _seen = new();

    public BrewSession(IBrewGame game, WebPage page, Func<string, bool, bool> toast, Func<int, (string Place, int Bottles)?> putAway, Func<bool> isBrewScreenOpen, Action<string> log)
    {
        _game = game;
        _page = page;
        _toast = toast;
        _putAway = putAway;
        _isBrewScreenOpen = isBrewScreenOpen;
        _log = log;
    }

    private static bool IsChinese => GameLanguage.IsChinese();

    /// <summary>Called on the main thread at start and after each settings change; the page hears them at the next refresh.</summary>
    public void UseSettings(bool isSpoilingFirst, bool isPuttingAway)
    {
        _isSpoilingFirst = isSpoilingFirst;
        _isPuttingAway = isPuttingAway;
        ForgetSent();
    }

    public void Update()
    {
        var now = Time.unscaledTime;

        _page.KeepInstalled(now);

        while (PageMessages.TryTake(out var command)) HandleSafely(command);

        if (now >= _nextRefreshAt || HasScreenFlipped()) Refresh(now);
    }

    /// <summary>The brewing screen opening or closing is read at once, so the view is drawn without waiting.</summary>
    private bool HasScreenFlipped()
    {
        var isOpen = _isBrewScreenOpen();

        if (isOpen == _wasScreenOpen) return false;

        _wasScreenOpen = isOpen;

        // A closed screen takes its search box with it, and may not say that typing ended.
        if (!isOpen) _keyboard.Release();

        return true;
    }

    /// <summary>One command that throws must not cost the rest of the frame its commands.</summary>
    private void HandleSafely(PageCommand command)
    {
        try
        {
            Handle(command);
        }
        catch (Exception ex)
        {
            _log($"page command {command} failed: {ex}");
            _page.Status(Texts.Failed.Pick(IsChinese));
        }
    }

    private void Handle(PageCommand command)
    {
        switch (command)
        {
            case PageCommand.Ready:
                _log("page script ready");
                _page.ForgetWords();
                _sentCatalog = null;
                ForgetSent();
                break;
            case PageCommand.NoScript:
                _page.Install();
                break;
            case PageCommand.Note note:
                _log("page: " + note.Text);
                break;
            case PageCommand.Focus focus:
                if (focus.IsTyping) _keyboard.Claim();
                else _keyboard.Release();
                break;
            case PageCommand.Load load:
                Press($"load {load.Tier}", () => LoadOpen(load.Tier));
                break;
            case PageCommand.Add add:
                Press($"add {add.ItemId}", () => BrewRun.Add(_game, add.ItemId, _isSpoilingFirst, IsChinese));
                break;
            case PageCommand.Remove remove:
                Press($"remove {remove.ItemId}", () => BrewRun.Remove(_game, remove.ItemId, IsChinese));
                break;
            case PageCommand.Empty:
                Press("empty", () => BrewRun.Empty(_game, IsChinese));
                break;
            case PageCommand.Collect collect:
                Press($"collect {collect.FurnitureId}", () => BrewRun.Collect(_game, collect.FurnitureId, IsChinese));
                break;
            case PageCommand.CollectAgain:
                Press("collect again", CollectAgain);
                break;
            case PageCommand.Rebrew rebrew:
                Press($"again {rebrew.FurnitureId} {rebrew.IsOn}", () => SetAgain(rebrew.FurnitureId, rebrew.IsOn));
                break;
            default:
                throw new InvalidOperationException("unhandled page command " + command);
        }
    }

    private Pressed LoadOpen(int tier)
    {
        if (_game.Read() is not BrewRead.Ready { Cellar: var cellar }) return new Pressed(false, Texts.NoGame.Pick(IsChinese));
        if (cellar.OpenId is not { } id) return new Pressed(false, Texts.NoBarrel.Pick(IsChinese));

        var pressed = BrewRun.Load(_game, id, tier, _isSpoilingFirst, IsChinese);

        if (pressed.IsDone) _lastTier[id] = tier;

        return pressed;
    }

    /// <summary>Collects the open barrel and brews the same drink again; its drink is the one it just made.</summary>
    private Pressed CollectAgain()
    {
        if (_game.Read() is not BrewRead.Ready { Cellar: var cellar }) return new Pressed(false, Texts.NoGame.Pick(IsChinese));
        if (cellar.Open is not { } barrel) return new Pressed(false, Texts.NoBarrel.Pick(IsChinese));

        var tier = TierOf(cellar, barrel);

        if (tier == null) return new Pressed(false, Texts.NotDone.Pick(IsChinese));

        // Collecting first would make the next read see this barrel go from done to idle and brew it again on its own.
        _seen.Remove(barrel.FurnitureId);

        var collected = BrewRun.Collect(_game, barrel.FurnitureId, IsChinese);

        if (!collected.IsDone) return collected;

        var stored = StoreBottles(barrel);
        var before = stored == null ? collected.Message : collected.Message + " " + stored;

        return BrewRun.LoadAndStart(_game, barrel.FurnitureId, tier.Value, _isSpoilingFirst, IsChinese, before);
    }

    private Pressed SetAgain(long furnitureId, bool isOn)
    {
        if (_game.Read() is not BrewRead.Ready { Cellar: var cellar }) return new Pressed(false, Texts.NoGame.Pick(IsChinese));
        if (cellar.BarrelOf(furnitureId) is not { } barrel) return new Pressed(false, Texts.NoBarrel.Pick(IsChinese));

        if (isOn) _again.Add(furnitureId);
        else _again.Remove(furnitureId);

        if (TierOf(cellar, barrel) is { } tier) _lastTier[furnitureId] = tier;

        return new Pressed(true, (isOn ? Texts.AgainOn : Texts.AgainOff).Say(IsChinese, barrel.Name));
    }

    /// <summary>The drink a barrel is making or made, else the one it was last filled for here.</summary>
    private int? TierOf(Cellar cellar, Barrel barrel) =>
        (barrel.State != BarrelState.Idle ? cellar.Catalog.TierOf(barrel.ResultItemId) : null) ?? (_lastTier.TryGetValue(barrel.FurnitureId, out var tier) ? tier : null);

    private void ForgetSent()
    {
        _shownState = "";
        _nextRefreshAt = 0f;
    }

    /// <summary>
    /// Reads the base once: the page gets the open barrel's state, and each barrel is compared with how it was last
    /// seen, so a finished barrel is told about and a collected one is brewed again when asked. With the brewing screen
    /// closed only the barrels are read: the page has nothing to draw, and planning every drink from every storage
    /// every few seconds was most of what this mod cost.
    /// </summary>
    private void Refresh(float now)
    {
        var isOpen = _isBrewScreenOpen();
        var read = isOpen ? _game.Read() : _game.ReadBarrels();

        if (read is BrewRead.NoGame) ForgetGame();

        var cellar = (read as BrewRead.Ready)?.Cellar;

        if (cellar != null) _catalog = cellar.Catalog;

        _nextRefreshAt = now + (cellar?.OpenId != null || _wasScreenOpen ? OpenRefreshSeconds : ClosedRefreshSeconds);

        if (cellar != null && Watch(cellar)) cellar = ((isOpen ? _game.Read() : _game.ReadBarrels()) as BrewRead.Ready)?.Cellar ?? cellar;

        SendState(isOpen ? cellar : null);
    }

    /// <summary>
    /// Looks at each barrel against how it was last seen. Brewing to done is told about; done to idle (collected, by the
    /// game's screen or here) puts the bottles away when that is on, then brews again when the barrel is set to. True
    /// when anything was pressed, so the caller reads again.
    /// </summary>
    private bool Watch(Cellar cellar)
    {
        var isPressed = false;

        foreach (var barrel in cellar.Barrels)
        {
            if (barrel.State != BarrelState.Idle && cellar.Catalog.TierOf(barrel.ResultItemId) is { } tier) _lastTier[barrel.FurnitureId] = tier;
            if (!_seen.TryGetValue(barrel.FurnitureId, out var before))
            {
                _seen[barrel.FurnitureId] = barrel;
                continue;
            }

            _seen[barrel.FurnitureId] = barrel;

            if (before.State == BarrelState.Brewing && barrel.State == BarrelState.Done) TellReady(cellar, barrel);
            if (before.State == BarrelState.Done && barrel.State == BarrelState.Idle) isPressed |= Collected(before);
        }

        return isPressed;
    }

    private void TellReady(Cellar cellar, Barrel barrel)
    {
        var drink = cellar.Catalog.Items.TryGetValue(barrel.ResultItemId, out var item) ? item.Name : Texts.Drink.Pick(IsChinese);

        Tell(Texts.BarrelReady.Say(IsChinese, barrel.Name, barrel.ResultCount, drink), false);
    }

    private bool Collected(Barrel before)
    {
        var stored = StoreBottles(before);

        if (stored != null) Tell(stored, false);
        if (!_again.Contains(before.FurnitureId) || !_lastTier.TryGetValue(before.FurnitureId, out var tier)) return stored != null;

        var pressed = BrewRun.LoadAndStart(_game, before.FurnitureId, tier, _isSpoilingFirst, IsChinese);

        _log($"brew again {before.FurnitureId}: {(pressed.IsDone ? "done" : "refused")} -> {pressed.Message}");
        Tell(Texts.BrewAgainStarted.Say(IsChinese, before.Name, pressed.Message), !pressed.IsDone);

        return true;
    }

    /// <summary>Puts a collected barrel's drink away when that is on; the line to show, or null when nothing moved.</summary>
    private string? StoreBottles(Barrel barrel)
    {
        if (!_isPuttingAway || barrel.ResultItemId <= 0 || _putAway(barrel.ResultItemId) is not { } stored) return null;

        var drink = (_catalog ?? Catalog.Empty).Items.TryGetValue(barrel.ResultItemId, out var item) ? item.Name : Texts.Drink.Pick(IsChinese);

        return Texts.BottlesStored.Say(IsChinese, stored.Bottles, drink, stored.Place);
    }

    /// <summary>A toast when Exiled's Toolkit can show one, else the page's own line.</summary>
    private void Tell(string text, bool isWarning)
    {
        _log(text);
        if (!_toast(text, isWarning)) _page.Status(text);
    }

    /// <summary>The state on the game's brewing screen, sent only when it changed. The catalog goes first, since the state names items by id.</summary>
    private void SendState(Cellar? cellar)
    {
        var state = cellar != null ? BrewView.State(cellar, new BrewExtras(_again, _isSpoilingFirst)) : BrewView.NoState();

        if (state == _shownState) return;
        if (cellar != null) SendCatalog(cellar.Catalog);
        if (_page.Brew(state)) _shownState = state;
    }

    private void SendCatalog(Catalog catalog)
    {
        if (ReferenceEquals(catalog, _sentCatalog)) return;
        if (_page.Catalog(BrewView.Catalog(catalog))) _sentCatalog = catalog;
    }

    /// <summary>A barrel's id means nothing in another save, so what is kept per barrel goes when the game is left.</summary>
    private void ForgetGame()
    {
        _again.Clear();
        _lastTier.Clear();
        _seen.Clear();
    }

    /// <summary>Runs one press and shows what it did, then reads again so the barrel shows the change at once.</summary>
    private void Press(string name, Func<Pressed> press)
    {
        string message;

        try
        {
            var pressed = press();

            message = pressed.Message;
            _log($"{name}: {(pressed.IsDone ? "done" : "refused")} -> {message}");
        }
        catch (Exception ex)
        {
            message = Texts.Failed.Pick(IsChinese);
            _log($"{name} failed: {ex}");
        }

        _page.Status(message);
        Refresh(Time.unscaledTime);
    }
}
