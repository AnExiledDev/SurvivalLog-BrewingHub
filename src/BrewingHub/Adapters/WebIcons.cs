using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BrewingHub.Core;
using UnityEngine;

namespace BrewingHub.Adapters;

/// <summary>
/// Indexes the icon files the game ships for its web UI, so the panel can show the same pictures. Walking the folders
/// (well over a thousand files) is done on a worker thread, so it never holds up a frame; only the folder's path is
/// asked of Unity, on the main thread.
/// </summary>
internal static class WebIcons
{
    private const string NoIcons = "no web icons, foods and dishes will show letters instead: ";

    /// <summary>Call on the main thread. The task never fails: a folder that cannot be read gives an empty index and says why.</summary>
    public static Task<(IconIndex Icons, string Note)> StartScan()
    {
        try
        {
            var webUi = Path.Combine(Application.streamingAssetsPath, "WebUI");

            return Task.Run(() => Scan(webUi));
        }
        catch (Exception ex)
        {
            return Task.FromResult((IconIndex.Empty, NoIcons + ex.Message));
        }
    }

    private static (IconIndex, string) Scan(string webUi)
    {
        var res = Path.Combine(webUi, "Res");

        try
        {
            var files = Directory.EnumerateFiles(res, "*.png", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(webUi, file)).ToList();

            return (IconIndex.Build(files), $"{files.Count} web icons found under {res}");
        }
        catch (Exception ex)
        {
            return (IconIndex.Empty, NoIcons + ex.Message);
        }
    }
}
