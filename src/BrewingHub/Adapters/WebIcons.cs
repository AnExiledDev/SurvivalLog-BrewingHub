using System;
using System.IO;
using System.Linq;
using BrewingHub.Core;
using UnityEngine;

namespace BrewingHub.Adapters;

/// <summary>Indexes the icon files the game ships for its web UI, so the panel can show the same pictures.</summary>
internal static class WebIcons
{
    public static IconIndex Load(Action<string> log)
    {
        var webUi = Path.Combine(Application.streamingAssetsPath, "WebUI");
        var res = Path.Combine(webUi, "Res");

        try
        {
            var files = Directory.EnumerateFiles(res, "*.png", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(webUi, file)).ToList();
            log($"{files.Count} web icons found under {res}");

            return IconIndex.Build(files);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"no web icons, foods and dishes will show letters instead: {ex.Message}");

            return IconIndex.Empty;
        }
    }
}
