using System.Reflection;
using System.Text.RegularExpressions;
using BrewingHub.Core;
using Xunit;

namespace BrewingHub.Tests;

/// <summary>What the page may send, and every word on screen in both languages.</summary>
public class ParseAndTextsTests
{
    private const string P = PageCommand.Prefix;

    [Fact]
    public void The_page_commands_parse_to_what_the_plugin_acts_on()
    {
        Assert.Equal(new PageCommand.Load(2), PageCommand.Parse(P + "load|2"));
        Assert.Equal(new PageCommand.Add(101), PageCommand.Parse(P + "add|101"));
        Assert.Equal(new PageCommand.Remove(101), PageCommand.Parse(P + "remove|101"));
        Assert.Equal(new PageCommand.Empty(), PageCommand.Parse(P + "empty"));
        Assert.Equal(new PageCommand.Collect(9007199254740993), PageCommand.Parse(P + "collect|9007199254740993"));
        Assert.Equal(new PageCommand.CollectAgain(), PageCommand.Parse(P + "collectagain"));
        Assert.Equal(new PageCommand.Rebrew(77, true), PageCommand.Parse(P + "rebrew|77|1"));
        Assert.Equal(new PageCommand.Focus(false), PageCommand.Parse(P + "focus|0"));
        Assert.Equal(new PageCommand.Note("x|y"), PageCommand.Parse(P + "diag|x|y"));
    }

    [Theory]
    [InlineData("load|3")]
    [InlineData("load|-1")]
    [InlineData("add|0")]
    [InlineData("add|abc")]
    [InlineData("collect|0")]
    [InlineData("rebrew|77|2")]
    [InlineData("empty|1")]
    [InlineData("cook|1")]
    public void Anything_else_is_ignored(string body)
    {
        Assert.Null(PageCommand.Parse(P + body));
    }

    [Fact]
    public void Messages_for_other_mods_are_not_ours()
    {
        Assert.Null(PageCommand.Parse("slmod|cookinghub|ready"));
        Assert.Null(PageCommand.Parse(null));
    }

    private static IEnumerable<Phrase> AllPhrases() =>
        typeof(Texts).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => field.GetValue(null)).OfType<Phrase>()
            .Concat(Texts.Page.Select(word => word.Phrase));

    private static string PageScriptText()
    {
        using var stream = typeof(ParseAndTextsTests).Assembly.GetManifestResourceStream("BrewingHub.page.js") ?? throw new InvalidOperationException("page.js is not embedded");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    [Fact]
    public void Every_text_has_plain_english_and_a_chinese_translation()
    {
        foreach (var phrase in AllPhrases())
        {
            var hasWords = Regex.Replace(phrase.En, @"\{\d\}", "").Any(char.IsLetter);

            Assert.True(phrase.En.All(ch => ch is >= ' ' and <= '~'), phrase.En);
            Assert.True(!hasWords || phrase.Zh.Any(ch => ch >= (char)0x4e00 && ch <= (char)0x9fff), phrase.Zh);
        }
    }

    [Fact]
    public void Both_languages_fill_in_the_same_blanks()
    {
        foreach (var phrase in AllPhrases())
        {
            for (var i = 0; i < 4; i++) Assert.True(phrase.En.Contains("{" + i + "}") == phrase.Zh.Contains("{" + i + "}"), phrase.En);
        }
    }

    [Fact]
    public void Page_keys_are_not_listed_twice()
    {
        Assert.Equal(Texts.Page.Count, Texts.Page.Select(w => w.Key).Distinct().Count());
    }

    /// <summary>page.js has no words of its own, so a key it asks for that the plugin never sends shows as the bare key.</summary>
    [Fact]
    public void Every_word_page_js_looks_up_exists()
    {
        var script = PageScriptText();
        var keys = Texts.Page.Select(w => w.Key).ToHashSet();
        var looked = Regex.Matches(script, @"say\('([A-Za-z0-9]+)'(?!\s*\+)").Select(m => m.Groups[1].Value);
        var tabled = Regex.Matches(script, @"'((?:tab|col|n)[A-Z][A-Za-z0-9]*|spoilFirst|freshFirst|rawOnly|haveOnly)'").Select(m => m.Groups[1].Value);
        var used = looked.Concat(tabled).Concat(new[] { "tier0", "tier1", "tier2" }).ToList();

        Assert.Contains("nRemove", used);
        Assert.Contains("colKeeps", used);

        foreach (var key in used) Assert.True(keys.Contains(key), key);
    }

    [Fact]
    public void The_page_script_is_plain_ascii()
    {
        var script = PageScriptText();

        Assert.True(script.All(ch => ch is '\n' or '\r' or (>= ' ' and <= '~')), "page.js has a character outside ASCII");
    }
}
