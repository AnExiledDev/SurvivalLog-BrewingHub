using BrewingHub.Core;
using ExiledToolkit.Web;
using Jint;

namespace BrewingHub.Tests;

/// <summary>
/// page.js exactly as the plugin embeds it, run in Jint with Exiled's Toolkit's page API (api.js, from the Toolkit's
/// source) on top of FakePage.js (Root.html, the game's brewing barrel screen and the vuplex bridge). The English words
/// go first, as BrewPage hands them over before its first call. What the script posts is parsed with the real
/// PageCommand, so a test sees the commands the plugin would act on.
/// </summary>
internal sealed class PageScript
{
    private readonly Engine _engine = new();

    /// <param name="isToolkitFirst">
    /// The game runs the Toolkit's script first, or with false Brewing Hub's first; then the Toolkit's runs only at
    /// <see cref="LoadToolkit"/>.
    /// </param>
    public PageScript(bool isToolkitFirst = true)
    {
        _engine.Execute(Resource("BrewingHub.Tests.FakePage.js"));
        _engine.Execute(PageWords.Script(isChinese: false));

        if (isToolkitFirst) _engine.Execute(Resource("ExiledToolkit.api.js"));

        _engine.Execute(Resource("BrewingHub.page.js"));
        Hear();
    }

    /// <summary>The Toolkit's page API arriving after Brewing Hub's script.</summary>
    public void LoadToolkit() => Run(Resource("ExiledToolkit.api.js"));

    /// <summary>Every command the script sent the plugin, its ready message and diag notes aside.</summary>
    public List<PageCommand> Sent { get; } = new();

    public List<string> Notes { get; } = new();

    public int Readies { get; private set; }

    public void Run(string statements)
    {
        _engine.Execute(statements);
        Hear();
    }

    public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    public bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    public int Count(string expression) => (int)_engine.Evaluate($"({expression}).length").AsNumber();

    /// <summary>One run of the Toolkit's 250 ms page check, which hands the brewing screen to the script.</summary>
    public void Poll() => Run("poll()");

    public void OpenBrewing() => Run("openBrewing()");

    public void Click(string expression) => Run($"click({expression})");

    public void Catalog(string catalogJson) => Call($"catalog({catalogJson})");

    public void Brew(string stateJson) => Call($"brew({stateJson})");

    public void Status(string text) => Call($"status({JsText.Literal(text)})");

    private void Call(string expression) => Run("window.__slbrewinghub." + expression);

    private void Hear()
    {
        var count = (int)_engine.Evaluate("posted.length").AsNumber();
        var messages = Enumerable.Range(0, count).Select(i => _engine.Evaluate($"posted[{i}]").AsString()).ToList();

        _engine.Execute("posted.length = 0");

        foreach (var message in messages)
        {
            switch (PageCommand.Parse(message) ?? throw new InvalidOperationException("the page sent a message the plugin cannot read: " + message))
            {
                case PageCommand.Ready:
                    Readies++;
                    break;
                case PageCommand.Note note:
                    Notes.Add(note.Text);
                    break;
                case var command:
                    Sent.Add(command);
                    break;
            }
        }
    }

    private static string Resource(string name)
    {
        using var stream = typeof(PageScript).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
