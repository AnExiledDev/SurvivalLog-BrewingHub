using System;
using System.Collections.Generic;

namespace BrewingHub.Core;

/// <summary>
/// Finds an item's picture among the game's web icons (WebUI/Res/&lt;Folder&gt;/&lt;name&gt;.png). The tables name an
/// icon in a form the web pages turn into a path themselves (such as
/// "../../Res/Furniture/UI_Item_Icon_Furnit_M_831300313.png"), so the panel matches on the file name alone.
/// </summary>
public sealed class IconIndex
{
    public static readonly IconIndex Empty = new(new Dictionary<string, List<string>>());

    private readonly IReadOnlyDictionary<string, List<string>> _pathsByName;

    private IconIndex(IReadOnlyDictionary<string, List<string>> pathsByName)
    {
        _pathsByName = pathsByName;
    }

    /// <summary>Indexes icon files given relative to the WebUI folder, such as "Res/Furniture/UI_Item_Icon_Furnit_M_x.png".</summary>
    public static IconIndex Build(IEnumerable<string> relativePaths)
    {
        var pathsByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in relativePaths)
        {
            var name = FileStem(path);

            if (!pathsByName.TryGetValue(name, out var paths)) pathsByName[name] = paths = new List<string>();

            paths.Add(path.Replace('\\', '/'));
        }

        return new IconIndex(pathsByName);
    }

    /// <summary>The page-relative path of the icon a table names, or null when the game has no such web icon.</summary>
    public string? Resolve(string? named)
    {
        if (string.IsNullOrWhiteSpace(named) || !_pathsByName.TryGetValue(FileStem(named), out var paths)) return null;

        var hint = named.Replace('\\', '/');

        return paths.Find(path => hint.EndsWith(path, StringComparison.OrdinalIgnoreCase)) ?? paths[0];
    }

    private static string FileStem(string path)
    {
        var name = path.Substring(path.Replace('\\', '/').LastIndexOf('/') + 1);
        var dot = name.LastIndexOf('.');

        return dot > 0 ? name.Substring(0, dot) : name;
    }
}
