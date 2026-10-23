using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;

namespace BrewingHub.Adapters;

/// <summary>
/// Sends the keyboard to the web UI while the brewing screen's search box has focus, the way the game's own crafting
/// search does, and gives it back as it was found.
/// </summary>
internal sealed class WebKeyboard
{
    private bool? _wasEnabled;

    /// <summary>True while a page field holds the keyboard, so its keys, Esc included, are the page's to handle.</summary>
    public bool IsClaimed => _wasEnabled != null;

    public void Claim()
    {
        var layer = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer();

        if (layer == null) return;

        _wasEnabled ??= layer.IsKeyboardEnabled;
        layer.SetKeyboardEnabled(true);
    }

    public void Release()
    {
        if (_wasEnabled is not { } wasEnabled) return;

        _wasEnabled = null;
        GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.SetKeyboardEnabled(wasEnabled);
    }
}
