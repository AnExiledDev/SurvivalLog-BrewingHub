using System.Globalization;

namespace BrewingHub.Core;

/// <summary>A message from the page. Everything else the page could send is ignored.</summary>
public abstract record PageCommand
{
    public const string Prefix = "slmod|brewinghub|";

    private PageCommand()
    {
    }

    public sealed record Ready : PageCommand;

    public sealed record NoScript : PageCommand;

    /// <summary>A diagnostic line from the page script, for the log.</summary>
    public sealed record Note(string Text) : PageCommand;

    /// <summary>A text box in the page gained or lost focus, so the game's keys should pause or resume.</summary>
    public sealed record Focus(bool IsTyping) : PageCommand;

    /// <summary>Fill the open barrel for a drink (0 Poor, 1 Average, 2 Strong).</summary>
    public sealed record Load(int Tier) : PageCommand;

    /// <summary>Put one unit of a food in the open barrel.</summary>
    public sealed record Add(int ItemId) : PageCommand;

    /// <summary>Take one unit of a food out of the open barrel, to the bag.</summary>
    public sealed record Remove(int ItemId) : PageCommand;

    /// <summary>Everything in the open barrel to the bag.</summary>
    public sealed record Empty : PageCommand;

    /// <summary>Take a finished barrel's bottles.</summary>
    public sealed record Collect(long FurnitureId) : PageCommand;

    /// <summary>Take the open barrel's bottles, fill it again for the same drink, and start it.</summary>
    public sealed record CollectAgain : PageCommand;

    /// <summary>Turn "brew again when collected" on or off for a barrel.</summary>
    public sealed record Rebrew(long FurnitureId, bool IsOn) : PageCommand;

    public static bool IsOurs(string? message) => message != null && message.StartsWith(Prefix, System.StringComparison.Ordinal);

    public static PageCommand? Parse(string? message)
    {
        if (message == null || !IsOurs(message)) return null;

        var body = message.Substring(Prefix.Length);

        if (body.StartsWith("diag|", System.StringComparison.Ordinal)) return new Note(body.Substring("diag|".Length));

        var parts = body.Split('|');

        return (parts[0], parts.Length) switch
        {
            ("ready", 1) => new Ready(),
            ("noscript", 1) => new NoScript(),
            ("focus", 2) when IsSwitch(parts[1]) => new Focus(parts[1] == "1"),
            ("load", 2) when TryTier(parts[1], out var tier) => new Load(tier),
            ("add", 2) when TryId(parts[1], out var itemId) => new Add(itemId),
            ("remove", 2) when TryId(parts[1], out var itemId) => new Remove(itemId),
            ("empty", 1) => new Empty(),
            ("collect", 2) when TryLongId(parts[1], out var furnitureId) => new Collect(furnitureId),
            ("collectagain", 1) => new CollectAgain(),
            ("rebrew", 3) when TryLongId(parts[1], out var furnitureId) && IsSwitch(parts[2]) => new Rebrew(furnitureId, parts[2] == "1"),
            _ => null,
        };
    }

    private static bool IsSwitch(string text) => text is "0" or "1";

    private static bool TryTier(string text, out int tier) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out tier) && tier is >= 0 and <= 2;

    private static bool TryId(string text, out int id) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    private static bool TryLongId(string text, out long id) => long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
}
