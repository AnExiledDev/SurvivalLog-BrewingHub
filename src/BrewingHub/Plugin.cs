using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BrewingHub.Adapters;
using BrewingHub.Core;
using HarmonyLib;

namespace BrewingHub;

[BepInPlugin(Guid, "Brewing Hub", Version)]
[BepInDependency(OptionalLive.ToolkitGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(StackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.brewinghub";
    private const string Version = "0.1.4";
    private const string StackingGuid = "com.dev.survivallog.stacking";

    public override void Load()
    {
        if (!OptionalLive.IsToolkitInstalled) Log.LogInfo(OptionalLive.MissingNote);

        var isSpoilingFirst = Config.Bind("General", "FillSpoilingFirst", true,
            "When true, Load and the one-click adds fill the barrel with the food closest to spoiling first. When false, they use the freshest food first.\n" +
            "设为 true 时，「放入」和一键加入会优先使用快变质的食材。设为 false 时，优先使用最新鲜的食材。");
        var isPuttingAway = Config.Bind("General", "PutBottlesAway", false,
            "When true, collecting a barrel moves every bottle of that drink in your bag to a home storage that already holds it. With no such storage, they stay in your bag.\n" +
            "设为 true 时，取出一桶酒时会把背包里所有这种酒移到已经放有这种酒的家中储物里。没有这样的储物时，酒留在背包里。");
        var isLoggingToFile = Config.Bind("Diagnostics", "LogToFile", false, new BepInEx.Configuration.ConfigDescription(
            "When true, what Brewing Hub does is also written to BepInEx\\brewing-hub.log, which is kept between game starts. Turn it on when reporting a problem.\n" +
            "设为 true 时，Brewing Hub 的操作也会写入 BepInEx\\brewing-hub.log，该文件在重启游戏后保留。报告问题时再打开。", null, "ModManager.Advanced"));
        var log = new BrewLog(Log, Path.Combine(Paths.BepInExRootPath, "brewing-hub.log"));
        var harmony = new Harmony(Guid);
        var game = new GameBrewing(log.Write);
        var toasts = new BrewToasts(log.Write);
        var session = new BrewSession(
            game,
            new WebPage(log.Write),
            (text, isWarning) => toasts.Show(text, isWarning, GameLanguage.IsChinese()),
            game.PutAway,
            game.IsBrewScreenOpen,
            log.Write);

        OptionalLive.Watch(isLoggingToFile, isOn => log.IsFileOn = isOn);
        log.Write($"===== Brewing Hub {Version} started");
        PageMessages.Apply(harmony, log.Write);

        OptionalLive.Watch(() =>
        {
            session.UseSettings(isSpoilingFirst.Value, isPuttingAway.Value);
            log.Write($"spoiling first {isSpoilingFirst.Value}, put bottles away {isPuttingAway.Value}");
        }, isSpoilingFirst, isPuttingAway);

        BrewTicker.Session = session;
        BrewTicker.Log = log.Write;
        AddComponent<BrewTicker>();
    }
}
