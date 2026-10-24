using System.Collections.Generic;
using System.Globalization;

namespace BrewingHub.Core;

/// <summary>One piece of on-screen text in the two languages the game ships. {0}, {1} are filled in by Say.</summary>
public readonly record struct Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;

    public string Say(bool isChinese, params object[] args) => string.Format(CultureInfo.InvariantCulture, Pick(isChinese), args);
}

/// <summary>The words the mod puts on screen: press results, reasons, and the page's own labels.</summary>
public static class Texts
{
    private const string Log = "BepInEx/LogOutput.log";

    public static readonly Phrase NoGame = new("Load a game first.", "请先载入游戏。");
    public static readonly Phrase NotReady = new("Can't brew right now: {0}.", "现在无法酿酒：{0}。");
    public static readonly Phrase Away = new("you are away from home", "你不在家中");
    public static readonly Phrase ReadFailed = new("your food could not be read, see " + Log, "无法读取食材，详见 " + Log);
    public static readonly Phrase Failed = new("Brewing Hub failed, see " + Log + ".", "Brewing Hub 出错，详见 " + Log + "。");

    public static readonly Phrase NoBarrel = new("Open a brewing barrel first.", "请先打开酿酒桶。");
    public static readonly Phrase Busy = new("This barrel is brewing.", "这个酿酒桶正在酿造。");
    public static readonly Phrase CollectFirst = new("Collect the bottles first.", "请先取出酒瓶。");
    public static readonly Phrase NotDone = new("This barrel has nothing ready.", "这个酿酒桶还没有酿好。");
    public static readonly Phrase NotEnough = new("Not enough fresh food for a barrel of {0}.", "新鲜食材不够酿一桶{0}。");
    public static readonly Phrase EmptyRefused = new("The food in the barrel could not go back to your bag, so nothing was loaded.", "桶里的食材放不回背包，所以没有放入新食材。");
    public static readonly Phrase Filled = new("The barrel is filled for {0}.", "已为{0}装好酿酒桶。");
    public static readonly Phrase Overfilled = new("Moved {0} where {1} was planned; check the barrel before brewing.", "放入了 {0} 份，计划是 {1} 份，开酿前请检查酿酒桶。");
    public static readonly Phrase MovedSome = new("Moved {0} of {1}; the game refused the rest.", "已放入 {0}/{1} 份，其余被游戏拒绝。");
    public static readonly Phrase NoneLeft = new("No fresh {0} left.", "没有新鲜的{0}了。");
    public static readonly Phrase MoveRefused = new("The game refused to move that.", "游戏拒绝了这次移动。");
    public static readonly Phrase Added = new("Added {0} from {1}.", "已从{1}加入{0}。");
    public static readonly Phrase NotInBarrel = new("No {0} in the barrel.", "桶里没有{0}。");
    public static readonly Phrase Removed = new("Took {0} out to your bag.", "已把{0}取回背包。");
    public static readonly Phrase AlreadyEmpty = new("The barrel is empty.", "酿酒桶是空的。");
    public static readonly Phrase Emptied = new("Emptied the barrel to your bag.", "已把桶里的食材全部取回背包。");
    public static readonly Phrase CollectRefused = new("The game gave no bottles.", "游戏没有给出酒瓶。");
    public static readonly Phrase Collected = new("Collected {0} {1}.", "已取出 {0} 瓶{1}。");
    public static readonly Phrase NotStarted = new("The barrel is filled but did not start. Start it by hand.", "已装好酿酒桶，但没有开始酿造。请手动开始。");
    public static readonly Phrase Started = new("Started a barrel of {0}.", "已开始酿一桶{0}。");
    public static readonly Phrase Drink = new("drink", "酒");
    public static readonly Phrase AgainOn = new("{0} will brew again when collected.", "{0}取出后会再酿一桶。");
    public static readonly Phrase AgainOff = new("{0} will not brew again.", "{0}不会再酿。");

    public static readonly Phrase ToastSource = new("Brewing Hub", "酿酒中心");
    public static readonly Phrase BarrelReady = new("{0} is ready: {1} {2}.", "{0}已酿好：{1} 瓶{2}。");
    public static readonly Phrase BrewAgainStarted = new("{0}: {1}", "{0}：{1}");
    public static readonly Phrase BottlesStored = new("Put {0} {1} away in {2}.", "已把 {0} 瓶{1}放进{2}。");

    public static readonly Phrase Bag = new("Bag", "背包");
    public static readonly Phrase Storage = new("Storage", "储物");
    public static readonly Phrase Barrel = new("Brewing Barrel", "酿酒桶");

    /// <summary>The words page.js draws on the brewing screen with, by the key it looks them up by.</summary>
    public static readonly IReadOnlyList<(string Key, Phrase Phrase)> Page = new (string, Phrase)[]
    {
        ("title", new("Brewing Hub", "酿酒中心")),
        ("tabNow", new("Brew now", "现在能酿")),
        ("tabRecipes", new("Recipes", "酒谱")),
        ("tabIngs", new("Ingredients", "食材")),
        ("tabBarrels", new("Barrels", "酿酒桶")),
        ("tier0", new("Poor", "劣质")),
        ("tier1", new("Average", "普通")),
        ("tier2", new("Strong", "烈性")),
        ("quality", new("Quality", "品质")),
        ("morale", new("Morale", "心态")),
        ("satiety", new("Satiety", "饱腹")),
        ("addFood", new("add food to see the drink", "放入食材后显示酿出的酒")),
        ("nextAt", new("{0} at {1}", "{1} 起为{0}")),
        ("best", new("best drink", "最好的酒")),
        ("over", new("over {0} satiety", "共 {0} 饱腹")),
        ("wasted", new("{0} satiety fills no bottle", "{0} 饱腹装不满一瓶")),
        ("moreFills", new("{0} more fills bottle {1}", "再加 {0} 可装满第 {1} 瓶")),
        ("full", new("the barrel is full at {0} bottles", "酿酒桶最多 {0} 瓶，已满")),
        ("tooLittle", new("needs {0} satiety to start", "至少需要 {0} 饱腹才能开始")),
        ("below", new("below {0}", "低于 {0}")),
        ("orMore", new("{0}+", "{0} 及以上")),
        ("readyIn", new("Ready in {0}", "{0}后酿好")),
        ("startNow", new("Start now: ready in {0}", "现在开始：{0}后酿好")),
        ("toBag", new("to bag", "回背包")),
        ("nAdd", new("Add {0} {1}: {2} x{3}", "加入 {0} 份{1}：{2} x{3}")),
        ("nRemove", new("Take out {0} {1}: {2} x{3}", "取出 {0} 份{1}：{2} x{3}")),
        ("again", new("Collect and brew again", "取出并再酿一桶")),
        ("againWhen", new("Brew again when collected", "取出后再酿一桶")),
        ("empty", new("Empty to bag", "全部取回背包")),
        ("nowNote", new("The best barrel of each drink from your bag and every storage.", "用背包和所有储物能酿出的每种酒的最佳一桶。")),
        ("spoilFirst", new("Food that spoils soonest first.", "最快变质的食材优先。")),
        ("freshFirst", new("Freshest food first.", "最新鲜的食材优先。")),
        ("noPlan", new("Not enough fresh food for this one", "新鲜食材不够酿这种酒")),
        ("load", new("Load", "放入")),
        ("loadNote", new("Load puts what is in the barrel back in your bag first, then fills it from the places shown.", "「放入」会先把桶里的食材取回背包，再从显示的位置装桶。")),
        ("bottles", new("{0} bottles", "{0} 瓶")),
        ("hoursShort", new("{0}h", "{0}小时")),
        ("daysHours", new("{0}d {1}h", "{0}天{1}小时")),
        ("recipesNote", new("The barrel picks the drink by the mix's quality: total morale over total satiety. {0} satiety per bottle, {1} bottles at most.", "酿酒桶按品质决定酿出的酒：总心态除以总饱腹。每瓶 {0} 饱腹，最多 {1} 瓶。")),
        ("band", new("quality {0}", "品质 {0}")),
        ("canMake", new("can make {0}", "能酿 {0} 瓶")),
        ("noneNow", new("none now", "现在酿不了")),
        ("goodFor", new("Good for it", "适合的食材")),
        ("usedIn", new("Used in", "可用于")),
        ("asks", new("Asked for in {0} relief requests", "{0} 个救济委托需要它")),
        ("talents", new("With your talents", "计入天赋后")),
        ("speedCut", new("Brewing time -{0}%", "酿酒所需时间缩短{0}%")),
        ("perBottle", new("Satiety per bottle: {0}", "每瓶所需饱腹：{0}")),
        ("lines", new("Quality lines: {0} and {1}", "品质分界：{0} 和 {1}")),
        ("search", new("Search ingredients", "搜索食材")),
        ("rawOnly", new("Raw food", "生食材")),
        ("haveOnly", new("Have", "已有")),
        ("colIng", new("Ingredient", "食材")),
        ("colSat", new("Sat", "饱腹")),
        ("colMood", new("Mor", "心态")),
        ("colQual", new("Qual", "品质")),
        ("colPer", new("/btl", "每瓶")),
        ("colHave", new("Have", "已有")),
        ("colTo", new("to {0}", "到{0}")),
        ("colKeeps", new("stays {0}", "保持{0}")),
        ("ingsNote", new("{0} of {1} ingredients. Click a row to add one to the barrel.", "共 {1} 种食材，显示 {0} 种。点击一行可放一份进桶。")),
        ("barrelsNote", new("Every brewing barrel in your base.", "基地里的所有酿酒桶。")),
        ("emptyBarrel", new("Empty", "空")),
        ("nothing", new("Nothing brewing", "没有在酿")),
        ("hoursLeft", new("{0} left", "还剩 {0}")),
        ("ready", new("Ready", "已酿好")),
        ("collect", new("Collect", "取出")),
        ("thisOne", new("(this one)", "（当前）")),
        ("tipMorale", new("Morale {0}, quality {1}", "心态 {0}，品质 {1}")),
        ("tipAlone", new("Alone: {0}, {1} per bottle", "单独酿：{0}，每瓶 {1} 个")),
        ("startedWith", new("In the barrel: {0}", "桶中：{0}")),
    };
}
