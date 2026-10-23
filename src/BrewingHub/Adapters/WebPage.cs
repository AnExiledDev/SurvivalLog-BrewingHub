using System;
using System.IO;
using BrewingHub.Core;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using Vuplex.WebView;

namespace BrewingHub.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's own pages sit in. It
/// draws Brewing Hub onto the game's brewing screen. The web view can reload at any time and
/// lose the script, so it is checked for every couple of seconds and put back when the page says it is missing.
/// </summary>
internal sealed class WebPage
{
    private const float CheckSeconds = 2f;
    private const string ScriptResource = "BrewingHub.page.js";
    private const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + PageCommand.Prefix + "noscript')";

    private readonly Action<string> _log;
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;
    private bool? _wordsAreChinese;

    public WebPage(Action<string> log)
    {
        _log = log;
    }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run("window.__slbrewinghub||" + NoScriptReply);
    }

    public void Install()
    {
        _wordsAreChinese = null;

        if (Run(Script())) _log("page script sent to the web UI");
    }

    /// <summary>The page lost its script, or loaded it afresh, so its words go with the next call.</summary>
    public void ForgetWords() => _wordsAreChinese = null;

    /// <summary>A result line, shown as a toast over the game.</summary>
    public void Status(string text) => Call($"status({JsText.Literal(text)})");

    /// <summary>Brewable foods, drinks, and names and icons of the items they name; the state refers to them by id only.</summary>
    public bool Catalog(string catalogJson) => Call($"catalog({catalogJson})");

    /// <summary>The open barrel's state drawn on the game's own brewing screen, or a view with show false to clear it.</summary>
    public bool Brew(string stateJson) => Call($"brew({stateJson})");

    /// <summary>
    /// The page's words go with the first call after a load and again whenever the game's language changes; resending
    /// the whole table with every call was most of what each refresh cost the web view.
    /// </summary>
    private bool Call(string expression)
    {
        var isChinese = GameLanguage.IsChinese();
        var words = _wordsAreChinese == isChinese ? "" : PageWords.Script(isChinese);
        var isRun = Run($"{words}window.__slbrewinghub?window.__slbrewinghub.{expression}:{NoScriptReply}");

        if (isRun) _wordsAreChinese = isChinese;

        return isRun;
    }

    private bool Run(string javaScript)
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not run script in the web UI (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
