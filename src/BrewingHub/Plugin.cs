using BepInEx;
using BepInEx.Unity.IL2CPP;
using BrewingHub.Adapters;
using ExiledToolkit;
using ExiledToolkit.Kit;

namespace BrewingHub;

[BepInPlugin(Guid, "Brewing Hub", Version)]
[BepInDependency(ToolkitGuid, ToolkitVersion)]
[BepInDependency(StackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.brewinghub";
    private const string Version = "0.3.0";

    /// <summary>Exiled's Toolkit: its shared helpers, toasts, live settings and UI size all come from it.</summary>
    private const string ToolkitGuid = "com.dev.survivallog.exiledtoolkit";
    private const string ToolkitVersion = ">=0.19.0";

    private const string StackingGuid = "com.dev.survivallog.stacking";
    private const string LogFileName = "brewing-hub.log";

    /// <summary>The name the page script hands its panels to the Toolkit's UI size with.</summary>
    private const string UiSizeName = "BrewingHub";

    public override void Load()
    {
        ModUiSize.Bind(Config, UiSizeName);

        var isSpoilingFirst = Config.Bind("General", "FillSpoilingFirst", true,
            "When true, the food closest to spoiling goes in first: Load picks it over another food that makes just as good a barrel, and the one-click adds take from it. When false, the freshest food goes in first. Either way, Load always picks the barrel with the most bottles from the fewest foods and the least waste.\n" +
            "设为 true 时，优先使用快变质的食材：两种食材能酿出一样好的一桶时，「放入」选快变质的那种，一键加入也从它取。设为 false 时，优先使用最新鲜的食材。无论哪种设置，「放入」都会选瓶数最多、所用食材最少、浪费最少的一桶。");
        var isPuttingAway = Config.Bind("General", "PutBottlesAway", false,
            "When true, collecting a barrel moves every bottle of that drink in your bag to a home storage that already holds it. With no such storage, they stay in your bag.\n" +
            "设为 true 时，取出一桶酒时会把背包里所有这种酒移到已经放有这种酒的家中储物里。没有这样的储物时，酒留在背包里。");
        var isLoggingToFile = Config.Bind("Diagnostics", "LogToFile", false, new BepInEx.Configuration.ConfigDescription(
            "When true, what Brewing Hub does is also written to BepInEx\\brewing-hub.log, which is kept between game starts. Turn it on when reporting a problem.\n" +
            "设为 true 时，Brewing Hub 的操作也会写入 BepInEx\\brewing-hub.log，该文件在重启游戏后保留。报告问题时再打开。", null, "ModManager.Advanced"));
        var log = new ModLog(Log, LogFileName);

        // Before the first line, since the file copy is on until the setting says otherwise.
        LiveSettings.Watch(isLoggingToFile, isOn => log.IsFileOn = isOn);

        var game = new GameBrewing(log.Write);
        var session = new BrewSession(
            game,
            new BrewPage(log.Write),
            game.PutAway,
            game.IsBrewScreenOpen,
            game.ReadScreen,
            game.Prepare,
            log.Write);

        log.Write($"===== Brewing Hub {Version} started");

        LiveSettings.Watch(() =>
        {
            session.UseSettings(isSpoilingFirst.Value, isPuttingAway.Value);
            log.Write($"spoiling first {isSpoilingFirst.Value}, put bottles away {isPuttingAway.Value}");
        }, isSpoilingFirst, isPuttingAway);

        BrewTicker.Session = session;
        BrewTicker.Log = log.Write;
        AddComponent<BrewTicker>();
        CellarWarmUp.Start(log.WriteFileOnly);
    }
}
