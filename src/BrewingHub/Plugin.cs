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
    private const string Version = "0.3.1";

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
            "When true, the food closest to spoiling goes in first. When false, the freshest food goes in first. Load always picks the barrel with the most bottles from the fewest foods and the least waste, so this only decides between foods that make an equally good barrel. Adding one food at a time, with a one-click button or a row on Ingredients, takes from the stack closest to spoiling, or the freshest.\n" +
            "设为 true 时，优先放入快变质的食材。设为 false 时，优先放入最新鲜的食材。「放入」总是选瓶数最多、所用食材最少、浪费最少的一桶，所以这项只在两种食材能酿出一样好的一桶时起作用。一次加入一份（一键按钮或在「食材」里点一行）时，也会从最快变质（或最新鲜）的那一堆取。");
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
