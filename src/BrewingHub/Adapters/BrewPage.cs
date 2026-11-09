using System;
using BrewingHub.Core;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Web;

namespace BrewingHub.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's pages sit in. It
/// draws Brewing Hub onto the game's brewing screen. The web view can reload at any time and lose the script, so
/// Exiled's Toolkit checks for it every couple of seconds and the session puts it back when the page says it is missing.
/// </summary>
internal sealed class BrewPage
{
    private const string ScriptResource = "BrewingHub.page.js";
    private const string Global = "window.__slbrewinghub";

    private readonly WebPage _page;
    private readonly Action<string> _log;

    public BrewPage(Action<string> log)
    {
        _log = log;

        // The page's words go with the first call after a load and again whenever the game's language changes;
        // resending the whole table with every call was most of what each refresh cost the web view.
        _page = new WebPage(typeof(BrewPage).Assembly, ScriptResource, Global, PageCommand.Prefix + "noscript", log)
        {
            Words = PageWords.Script,
        };
    }

    public void KeepInstalled(float now) => _page.KeepInstalled(now);

    public void Install() => _page.Install();

    /// <summary>The page lost its script, or loaded it afresh, so its words go with the next call.</summary>
    public void ForgetWords() => _page.ForgetWords();

    /// <summary>What a press did, as an Exiled's Toolkit toast. The page hears it too, as it frees the pressed button.</summary>
    public void Status(string text, ToastKind kind)
    {
        _page.Call($"status({JsText.Literal(text)})");
        Toast(text, kind);
    }

    /// <summary>A message as an Exiled's Toolkit toast, without touching the page.</summary>
    public void Toast(string text, ToastKind kind)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (Toasts.Show(new ToastRequest(Texts.ToastSource.Pick(GameLanguage.IsChinese()), text) { Kind = kind }).IsNone) _log("a toast was not shown: " + text);
    }

    /// <summary>Brewable foods, drinks, and names and icons of the items they name; the state refers to them by id only.</summary>
    public bool Catalog(string catalogJson) => _page.Call($"catalog({catalogJson})");

    /// <summary>The open barrel's state drawn on the game's brewing screen, or a view with show false to clear it.</summary>
    public bool Brew(string stateJson) => _page.Call($"brew({stateJson})");
}
