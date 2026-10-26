using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BrewingHub.Core;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace BrewingHub.Adapters;

/// <summary>
/// The game's side of every read and press. Food is read the way the building menu counts materials (the player's bag
/// and every home storage in the cost pool); barrels are every brewing furniture in the base, read through their brew
/// component and the game's own preview; food moves by the quick move the brewing screen's shift-click ends in. Each
/// call fails soft: an exception is logged once per kind of call and answered as a refusal.
/// </summary>
internal sealed class GameBrewing : IBrewGame
{
    private const int SatietyValue = 1;
    private const int MoraleValue = 2;
    private const string SpeedTalent = "Buff/AE_BrewSpeedUp";
    private const float BarrelRescanSeconds = 30f;

    /// <summary>The share of a frame the catalog build may take.</summary>
    private static readonly long SliceTicks = Stopwatch.Frequency / 1000;

    private static Il2CppSystem.Type? _brewScreen;

    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();
    private readonly CatalogCache<(IntPtr Config, bool IsChinese)> _catalogs = new();
    private readonly FullReadClock _fullReads = new();
    private readonly StackMemo _stacks = new();
    private readonly Dictionary<int, Row> _rows = new();
    private readonly List<long> _barrelIds = new();
    private readonly List<Core.Stack> _storageStock = new();
    private readonly Func<bool> _isSliceSpent;

    private Task<(IconIndex Icons, string Note)>? _iconScan;
    private IconIndex? _icons;
    private IntPtr _rowsFor;
    private IntPtr _barrelsFor;
    private float _barrelsScannedAt;
    private Places? _places;
    private Rules? _rules;
    private bool _wasScreenOpen;
    private ulong _heldPrint;
    private long _sliceEndsAt;

    public GameBrewing(Action<string> log)
    {
        _log = log;
        _isSliceSpent = () => Stopwatch.GetTimestamp() >= _sliceEndsAt;
    }

    /// <summary>What a read covers.</summary>
    private enum ReadMode
    {
        /// <summary>Every storage and every barrel's contents: what a press needs.</summary>
        Full,

        /// <summary>The open brewing screen: the bag and the open barrel, with the storages as last read in full.</summary>
        Screen,

        /// <summary>The barrels alone, with no food: what watching for finished and collected barrels needs.</summary>
        Barrels,
    }

    private static Il2CppSystem.Type BrewScreen => _brewScreen ??= Il2CppType.Of<WebUI_Brew>();

    public BrewRead Read() => ReadSafely(ReadMode.Full);

    public BrewRead ReadBarrels() => ReadSafely(ReadMode.Barrels);

    /// <summary>
    /// What the session reads each refresh. With the brewing screen closed, the barrels alone. Open, the bag and the open
    /// barrel each time, and every storage when the screen has just opened, every few seconds, when the hour turns, and
    /// after a press.
    /// </summary>
    public BrewRead ReadScreen(bool isOpen)
    {
        if (isOpen && !_wasScreenOpen) _fullReads.Forget();

        _wasScreenOpen = isOpen;

        return ReadSafely(isOpen ? ReadMode.Screen : ReadMode.Barrels);
    }

    /// <summary>
    /// Runs the catalog build for a slice of this frame when one is under way. The tables only change with the
    /// language, but reading them all at once held up a frame for tens of milliseconds.
    /// </summary>
    public void Prepare()
    {
        if (!_catalogs.IsBuilding) return;

        try
        {
            _sliceEndsAt = Stopwatch.GetTimestamp() + SliceTicks;

            if (_catalogs.Advance(_isSliceSpent) is { } built) LogBuilt(built);
        }
        catch (Exception ex)
        {
            ReportOnce("catalog", ex);
        }
    }

    private BrewRead ReadSafely(ReadMode mode)
    {
        try
        {
            return ReadCellar(mode);
        }
        catch (Exception ex)
        {
            ReportOnce("read", ex);

            return new BrewRead.NotReady(Texts.ReadFailed.Pick(GameLanguage.IsChinese()));
        }
    }

    /// <summary>A whole stack by the game's quick move.</summary>
    public bool MoveStack(long instanceId, long fromOwnerId, long toOwnerId) => Press("move", () =>
    {
        var items = LoadedGame()?.World._ItemManager;

        if (items == null || !QuickMove(items, instanceId, fromOwnerId, toOwnerId)) return false;

        Ac_Brew_RefreshBag.SendAction();

        return true;
    });

    /// <summary>
    /// With Stacking, each unit goes in by its one-unit move. That move puts the unit in under the stack's own id and
    /// leaves the rest as a new stack in the same place, so the next unit comes off the stack that appeared there.
    /// Without Stacking, or for a single item, the stack goes in whole by the game's quick move.
    /// </summary>
    public int MoveUnits(long instanceId, long fromOwnerId, long toOwnerId, int units) => PressCount("move units", () =>
    {
        var items = LoadedGame()?.World._ItemManager;
        var moved = 0;
        var current = instanceId;

        while (items != null && moved < units && items.GetItemData(current) is { ItemCount: > 0 } item)
        {
            var configId = item.ItemConfigId;
            var count = item.ItemCount;
            var before = IdsOf(items, fromOwnerId, configId);
            var one = count >= 2 ? StackingUnits.MoveOne(current, fromOwnerId, toOwnerId) : null;

            if (one == null)
            {
                // The quick move takes the whole stack; never put in more than was asked.
                if (count > units - moved)
                {
                    _log($"stack {current} holds {count}, more than the {units - moved} asked, and cannot be split");

                    break;
                }

                if (QuickMove(items, current, fromOwnerId, toOwnerId)) moved += count - LeftIn(items, current, fromOwnerId);

                break;
            }

            if (one == false)
            {
                _log($"stacking did not move a unit of stack {current} from {fromOwnerId} to {toOwnerId}");

                break;
            }

            moved++;
            current = NewId(IdsOf(items, fromOwnerId, configId), before);
        }

        if (moved > 0) Ac_Brew_RefreshBag.SendAction();

        return moved;
    });

    /// <summary>What is still in the source after a move: a quick move can be cut short by another mod.</summary>
    private static int LeftIn(ItemManager items, long instanceId, long ownerId) =>
        items.GetItemData(instanceId) is { ItemCount: > 0 } item && item.OwnerId == ownerId ? item.ItemCount : 0;

    /// <summary>Furniture.OnBrewStart, what the screen's start button ends in. False when the game refused.</summary>
    public bool Start(long furnitureId) => Press("start", () =>
    {
        var furniture = FurnitureOf(LoadedGame()?.Agents, furnitureId);
        var isStarted = furniture != null && furniture.OnBrewStart();

        if (!isStarted) _log($"the game did not start barrel {furnitureId}");

        return isStarted;
    });

    /// <summary>
    /// The screen's harvest: the game's own check first (a full bag, say), whose reason is shown as the game words it,
    /// then the harvest into the player's bag.
    /// </summary>
    public Collected Collect(long furnitureId)
    {
        _fullReads.Forget();

        try
        {
            var game = LoadedGame();
            var furniture = FurnitureOf(game?.Agents, furnitureId);

            if (game == null || furniture == null) return new Collected(0, null);

            var roleId = game.Value.Role.InstanceId;
            var blocked = furniture.CheckBrewHarvestBlocked(roleId);

            if (blocked != 0) return new Collected(0, NonEmpty(Furniture.GetBrewHarvestBlockedText(blocked)) ?? Texts.CollectRefused.Pick(GameLanguage.IsChinese()));

            var bottles = furniture.OnBrewHarvest(roleId);

            if (bottles > 0) Ac_Brew_RefreshBag.SendAction();

            return new Collected(bottles, null);
        }
        catch (Exception ex)
        {
            ReportOnce("collect", ex);

            return new Collected(0, null);
        }
    }

    /// <summary>Furniture.OnBrewBagChanged, what the game runs after a move in or out of a barrel; it refreshes the barrel's screen.</summary>
    public void Changed(long furnitureId)
    {
        _fullReads.Forget();

        try
        {
            FurnitureOf(LoadedGame()?.Agents, furnitureId)?.OnBrewBagChanged();

            if (IsBrewScreenOpen()) Ac_Brew_RefreshBag.SendAction();
        }
        catch (Exception ex)
        {
            ReportOnce("refresh", ex);
        }
    }

    /// <summary>
    /// Every bottle of the drink in the bag goes to a home storage that already holds that drink, so a collected barrel
    /// ends up beside the earlier ones. Returns the place and how many bottles went, or null when none holds it or the
    /// game refused.
    /// </summary>
    public (string Place, int Bottles)? PutAway(int drinkId)
    {
        _fullReads.Forget();

        try
        {
            var game = LoadedGame();

            if (game == null) return null;

            var (_, config, agents, role) = game.Value;
            var items = game.Value.World._ItemManager;
            var available = items?.GetAllAvailableItems(role.InstanceId);
            long? target = null;

            for (var i = 0; available != null && i < available.Count; i++)
            {
                if (available[i] is { } item && item.ItemConfigId == drinkId && item.OwnerId != role.InstanceId && !IsSetApart(item.OwnerId) && !Furniture.IsBrewBarrelOwner(item.OwnerId))
                {
                    target = item.OwnerId;

                    break;
                }
            }

            if (items == null || target is not { } to) return null;

            var bottles = 0;
            var held = items.GetItemDataList(role.InstanceId);
            var stacks = new List<(long Id, int Count)>();

            for (var i = 0; held != null && i < held.Count; i++)
            {
                if (held[i] is { } item && item.ItemConfigId == drinkId) stacks.Add((item.InstanceId, item.ItemCount));
            }

            foreach (var (id, count) in stacks)
            {
                if (items.TryQuickMoveItem(id, role.InstanceId, to, true)) bottles += count - LeftIn(items, id, role.InstanceId);
            }

            if (bottles <= 0) return null;

            Ac_Brew_RefreshBag.SendAction();

            return (PlacesFor(config, agents, role.InstanceId, GameLanguage.IsChinese()).Name(to), bottles);
        }
        catch (Exception ex)
        {
            ReportOnce("put away", ex);

            return null;
        }
    }

    /// <summary>True while the game's brewing screen is up.</summary>
    public bool IsBrewScreenOpen()
    {
        try
        {
            return GameSingleton.Existing<ReduxUISystem>() is { } ui && ui.IsUIActive(BrewScreen);
        }
        catch (Exception ex)
        {
            ReportOnce("brewing screen check", ex);

            return false;
        }
    }

    private BrewRead ReadCellar(ReadMode mode)
    {
        var game = LoadedGame();

        if (game == null)
        {
            ForgetGame();

            return new BrewRead.NoGame();
        }

        var (world, config, agents, role) = game.Value;
        var isChinese = GameLanguage.IsChinese();

        if (!IsHome(agents)) return new BrewRead.NotReady(Texts.Away.Pick(isChinese));

        var items = world._ItemManager;

        if (items == null) return new BrewRead.NotReady(Texts.ReadFailed.Pick(isChinese));

        var hour = world._GameTimeManager?.GetHour() ?? 0;
        var now = UnityEngine.Time.unscaledTime;
        var roleId = role.InstanceId;
        var openId = mode == ReadMode.Barrels ? null : OpenBarrelId();
        var furnitures = BarrelFurnitures(agents, openId, now);
        var catalog = CatalogFor(config, furnitures.Count > 0 ? furnitures[0] : null, isChinese, mode);

        // Only with the screen closed, until the first catalog is built a moment after a game loads; nothing reads this reason.
        if (catalog == null) return new BrewRead.NotReady(Texts.ReadFailed.Pick(isChinese));

        var places = PlacesFor(config, agents, roleId, isChinese);
        var isFull = mode == ReadMode.Full;

        if (mode != ReadMode.Barrels) _stacks.BeginSweep();

        var nowHour = Furniture.GetBrewNowHour();
        var barrels = new List<Barrel>(furnitures.Count);
        Furniture? open = null;
        Barrel? openBarrel = null;

        foreach (var furniture in furnitures)
        {
            var id = furniture.InstanceId;
            var barrel = ReadBarrel(items, config, furniture, id, hour, nowHour, places, withContents: isFull || id == openId);

            if (id == openId) (open, openBarrel) = (furniture, barrel);

            barrels.Add(barrel);
        }

        var isEverything = false;
        var stock = mode == ReadMode.Barrels ? new List<Core.Stack>() : ReadFood(items, config, roleId, hour, now, places, openBarrel, isFull, out isEverything);
        var rules = isEverything || _rules == null ? _rules = ReadRules(config) : _rules;

        return new BrewRead.Ready(new Cellar(roleId, rules, catalog, stock, barrels, open == null ? null : openId, open == null ? Mix.None : Preview(open), StackingUnits.IsPresent));
    }

    /// <summary>
    /// The food the player can reach. A full read walks every storage. The open screen reads the bag each time and keeps
    /// the storages as last walked; it walks them again every few seconds, when the hour turns, after a press, and when
    /// the bag or the open barrel changed, since food moved by hand on the game's own screen may have left a storage.
    /// </summary>
    private List<Core.Stack> ReadFood(ItemManager items, ConfigManager config, long roleId, int hour, float now, Places places, Barrel? open, bool isFull, out bool isEverything)
    {
        if (!isFull)
        {
            var bag = ReadBag(items, config, roleId, hour, places);
            var held = StatePrint.OfHeld(bag, roleId, open);
            var isHeldSame = held == _heldPrint;

            _heldPrint = held;

            if (isHeldSame && !_fullReads.IsDue(now, hour))
            {
                isEverything = false;
                bag.AddRange(_storageStock);

                return bag;
            }
        }

        var stock = ReadStock(items, config, roleId, hour, places);

        _stacks.EndSweep();
        _fullReads.Read(now, hour);
        _heldPrint = StatePrint.OfHeld(stock, roleId, open);
        isEverything = true;

        return stock;
    }

    private static (BattleLogicWorld World, ConfigManager Config, AgentManager Agents, LeadingRole Role)? LoadedGame()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var config = GameSingleton.Existing<ConfigManager>();
        var agents = world?._AgentManager;
        var role = agents?.GetLeadingRole();

        if (world == null || config == null || agents == null || role == null || world._GameTimeManager?.WorldStateType != WorldStateType.PostDisaster) return null;

        return (world, config, agents, role);
    }

    /// <summary>A barrel's id, a stack's id and a storage's name mean nothing in another save.</summary>
    private void ForgetGame()
    {
        _stacks.Forget();
        _storageStock.Clear();
        _barrelIds.Clear();
        _fullReads.Forget();
        _places = null;
        _rules = null;
    }

    /// <summary>
    /// The brewing furniture in the base, in the game's order. Walking every piece of furniture is the costly part, so
    /// the barrels found are kept by id and looked up directly; the base is walked again when one is gone, when the open
    /// screen is a barrel not among them, and every half minute for barrels built since.
    /// </summary>
    private List<Furniture> BarrelFurnitures(AgentManager agents, long? openId, float now)
    {
        var isStale = agents.Pointer != _barrelsFor || now < _barrelsScannedAt || now - _barrelsScannedAt >= BarrelRescanSeconds || (openId is { } id && !_barrelIds.Contains(id));

        if (!isStale)
        {
            var known = new List<Furniture>(_barrelIds.Count);

            foreach (var barrelId in _barrelIds)
            {
                if (agents.GetAgent(barrelId)?.TryCast<Furniture>() is not { } furniture) break;

                known.Add(furniture);
            }

            if (known.Count == _barrelIds.Count) return known;
        }

        var barrels = new List<Furniture>();
        var furnitures = agents.GetAllFurnitures();

        _barrelIds.Clear();

        for (var i = 0; furnitures != null && i < furnitures.Count; i++)
        {
            if (furnitures[i] is { } furniture && furniture.IsBrewFurniture())
            {
                barrels.Add(furniture);
                _barrelIds.Add(furniture.InstanceId);
            }
        }

        _barrelsFor = agents.Pointer;
        _barrelsScannedAt = now;

        return barrels;
    }

    /// <summary>The barrel whose brewing screen is open, from the screen's own state, or null.</summary>
    private static long? OpenBarrelId()
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screen = BrewScreen;

        if (ui == null || !ui.IsUIActive(screen)) return null;

        var state = ui.GetStateByUI(screen)?.TryCast<State_Web_Brew>();

        return state is { CurrentFurnitureId: > 0 } ? state.CurrentFurnitureId : null;
    }

    /// <summary>The open barrel's mix as the game adds it up for its own screen.</summary>
    private static Mix Preview(Furniture furniture)
    {
        furniture.CalcBrewPreview(out var count, out var sat, out var density, out _, out _, out _, out _);

        return count > 0 ? new Mix(sat, density * sat, count) : Mix.None;
    }

    /// <summary>A barrel; what is in it is read only when asked for, since the page shows only the open barrel's.</summary>
    private Barrel ReadBarrel(ItemManager items, ConfigManager config, Furniture furniture, long id, int hour, int nowHour, Places places, bool withContents)
    {
        var brew = AgentTools.GetAgentComponent<BrewComponent>(furniture);
        var name = places.Name(id);
        IReadOnlyList<Core.Stack> contents = Array.Empty<Core.Stack>();

        if (withContents)
        {
            var read = new List<Core.Stack>();
            var held = items.GetItemDataList(id);

            for (var i = 0; held != null && i < held.Count; i++)
            {
                if (held[i] is { } item && StackOf(item, config, hour, name) is { } stack) read.Add(stack);
            }

            contents = read;
        }

        var state = brew == null ? BarrelState.Idle : (BarrelState)Math.Clamp(brew.State, 0, 2);

        return new Barrel(
            id,
            name,
            state,
            furniture.IsBrewLocked(),
            brew?.ResultItemId ?? 0,
            brew?.ResultCount ?? 0,
            brew == null || state != BarrelState.Brewing ? 0 : brew.GetRemainHours(nowHour),
            brew?.TotalHours ?? 0,
            contents);
    }

    /// <summary>
    /// Brewable food the player can reach: their bag and every home storage the game counts for building costs,
    /// skipping storage the game keeps out of that pool, anything already in a barrel, and every stove's pot: taking food
    /// out of a cooking pot makes the game stop that stove (Cooking_RunningState.Update, CancelCookingByIngredientLost).
    /// The game lists the bag first, then the storages; the storages are kept for the reads in between.
    /// </summary>
    private List<Core.Stack> ReadStock(ItemManager items, ConfigManager config, long roleId, int hour, Places places)
    {
        var stock = new List<Core.Stack>();
        var available = items.GetAllAvailableItems(roleId);

        places.ForgetSetApart();
        _storageStock.Clear();

        for (var i = 0; available != null && i < available.Count; i++)
        {
            if (available[i] is not { } item) continue;

            var ownerId = item._OwnerId_k__BackingField;
            var isBag = ownerId == roleId;

            if (!IsBrewable(item, config)) continue;
            if (!isBag && places.IsSetApartOrBarrel(ownerId)) continue;
            if (StackOf(item, config, hour, places.Name(ownerId)) is not { } stack) continue;

            stock.Add(stack);

            if (!isBag) _storageStock.Add(stack);
        }

        return stock;
    }

    /// <summary>The brewable food in the bag as it is now; the game lists the bag before the storages, so the storages follow it.</summary>
    private List<Core.Stack> ReadBag(ItemManager items, ConfigManager config, long roleId, int hour, Places places)
    {
        var stock = new List<Core.Stack>();
        var held = items.GetItemDataList(roleId);
        var bag = places.Name(roleId);

        for (var i = 0; held != null && i < held.Count; i++)
        {
            if (held[i] is { } item && StackOf(item, config, hour, bag) is { } stack) stock.Add(stack);
        }

        return stock;
    }

    private bool IsBrewable(ItemData item, ConfigManager config) => item._ItemCount_k__BackingField > 0 && RowOf(config, item._ItemConfigId_k__BackingField).CanBrew;

    /// <summary>
    /// One stack of brewable food, or null for anything else, each unit giving what Furniture.CalcBrewPreview counts: the
    /// item's satiety (never below 0) and morale, each times the uses it has left (a part-used item gives less). Those
    /// depend only on the item and its uses, so they are asked of the game again only when one of those changed.
    /// </summary>
    private Core.Stack? StackOf(ItemData item, ConfigManager config, int hour, string place)
    {
        var count = item._ItemCount_k__BackingField;
        var configId = item._ItemConfigId_k__BackingField;

        if (count <= 0) return null;

        var row = RowOf(config, configId);

        if (!row.CanBrew || row.Item == null) return null;

        var instanceId = item._InstanceId_k__BackingField;
        var ownerId = item._OwnerId_k__BackingField;
        var useTimes = item._UseTimes_k__BackingField;
        var maxUseTimes = item._MaxUseTimes_k__BackingField;
        var hoursLeft = row.Spoils ? item._StartTime_k__BackingField + item._TimeLeft_k__BackingField - hour : (double?)null;

        if (_stacks.Find(instanceId, configId, useTimes, maxUseTimes, count, ownerId, place, hoursLeft) is { } known) return known;

        var uses = Furniture.GetBrewUseTimes(item, row.Item);
        var sat = Math.Max(0, ItemValueDisplayHelper.GetEffectiveVD(item, row.Item, SatietyValue)) * uses;
        var mood = ItemValueDisplayHelper.GetEffectiveVD(item, row.Item, MoraleValue) * uses;

        return _stacks.Keep(configId, useTimes, maxUseTimes, new Core.Stack(instanceId, configId, count, ownerId, place, hoursLeft, sat, mood));
    }

    /// <summary>An item row and what the reads ask of it, looked up once per game tables; the lookup logs nothing for a missing id.</summary>
    private Row RowOf(ConfigManager config, int id)
    {
        if (config.Pointer != _rowsFor)
        {
            _rows.Clear();
            _rowsFor = config.Pointer;
        }

        if (_rows.TryGetValue(id, out var known)) return known;

        var item = config._Config_Item_Dict is { } table && table.TryGetValue(id, out var found) ? found : null;

        return _rows[id] = item == null ? default : new Row(item, item.CanBrew, item.Life > 0);
    }

    /// <summary>The settings and talents CalcBrewPreview reads, through the game's own getters.</summary>
    private static Rules ReadRules(ConfigManager config)
    {
        var shipped = Rules.Shipped;

        Furniture.GetEffectiveTierThresholds(out var low, out var high);

        var perBottle = Furniture.GetEffectiveSatietyPerBottle();
        var maxBottles = (int)Furniture.GetBrewSetting("Brew_MaxBottle", shipped.MaxBottles);
        var perSatiety = config.TryGet_Config_GlobalSetting("Brew_HourPerSatiety")?.Params2;

        return new Rules(
            low,
            high,
            perBottle > 0 ? perBottle : shipped.PerBottle,
            maxBottles > 0 ? maxBottles : shipped.MaxBottles,
            Furniture.GetBrewSetting("Brew_MinSatiety", (float)shipped.MinSatiety),
            Furniture.GetBrewSetting("Brew_HourBase", (float)shipped.HourBase),
            perSatiety ?? shipped.HourPerSatiety,
            Furniture.GetBrewSetting("Brew_HourMax", (float)shipped.HourMax),
            Furniture.GetBrewTalentRatio(SpeedTalent));
    }

    /// <summary>
    /// The tables only change with the language, so the catalog is built once per game and language, a slice per frame
    /// (Prepare); until it is done the last one stands. The drinks come from a barrel, so a catalog built before the
    /// first barrel stood is built again once one does. A press needs the catalog now, and so does an open screen with
    /// none to show yet, so those finish the build at once.
    /// </summary>
    private Catalog? CatalogFor(ConfigManager config, Furniture? barrel, bool isChinese, ReadMode mode)
    {
        var key = (config.Pointer, isChinese);

        if (_catalogs.Built(key, barrel != null) is { } ready) return ready;

        var catalog = _catalogs.For(key, barrel != null, BuildStarter(config, barrel));
        var mustFinish = mode == ReadMode.Full || (mode == ReadMode.Screen && catalog == null);

        if (!mustFinish || !_catalogs.IsBuilding) return catalog;

        while (_catalogs.IsBuilding)
        {
            if (_catalogs.Advance(() => false) is { } built)
            {
                LogBuilt(built);

                return built;
            }

            // Only the icon step waits, on the scan's worker thread.
            _iconScan?.Wait();
        }

        return catalog;
    }

    /// <summary>Kept apart from CatalogFor so the read that finds the catalog built allocates no closure.</summary>
    private Func<CatalogBuild> BuildStarter(ConfigManager config, Furniture? barrel) => () =>
    {
        if (_icons == null) _iconScan ??= WebIcons.StartScan();

        return new CatalogBuild(new Tables(config, barrel, this));
    };

    private void LogBuilt(Catalog catalog) => _log($"read {catalog.Ingredients.Count} brewable foods and {catalog.Drinks.Count} drinks");

    /// <summary>The web icons once the worker thread has found them, else null; what it found is logged here, on the main thread.</summary>
    private IconIndex? IconsIfIn()
    {
        if (_icons != null) return _icons;

        _iconScan ??= WebIcons.StartScan();

        if (!_iconScan.IsCompleted) return null;

        var (icons, note) = _iconScan.Status == TaskStatus.RanToCompletion
            ? _iconScan.Result
            : (IconIndex.Empty, "no web icons, foods and dishes will show letters instead: " + _iconScan.Exception?.GetBaseException().Message);

        _icons = icons;
        _iconScan = null;
        _log(note);

        return _icons;
    }

    private Places PlacesFor(ConfigManager config, AgentManager agents, long roleId, bool isChinese)
    {
        if (_places == null || !_places.IsFor(config, agents, roleId, isChinese)) _places = new Places(config, agents, roleId, isChinese);

        return _places;
    }

    /// <summary>ItemManager.TryQuickMoveItem, what the brewing screen's shift-click ends in, without its messages.</summary>
    private bool QuickMove(ItemManager items, long instanceId, long fromOwnerId, long toOwnerId)
    {
        _fullReads.Forget();

        var isMoved = items.TryQuickMoveItem(instanceId, fromOwnerId, toOwnerId, true);

        if (!isMoved) _log($"the game did not move stack {instanceId} from {fromOwnerId} to {toOwnerId}");

        return isMoved;
    }

    private static HashSet<long> IdsOf(ItemManager items, long ownerId, int configId)
    {
        var ids = new HashSet<long>();
        var held = items.GetItemDataList(ownerId);

        for (var i = 0; held != null && i < held.Count; i++)
        {
            if (held[i] is { } item && item.ItemConfigId == configId) ids.Add(item.InstanceId);
        }

        return ids;
    }

    /// <summary>The stack that appeared since, or 0 when none did.</summary>
    private static long NewId(HashSet<long> now, HashSet<long> before)
    {
        foreach (var id in now)
        {
            if (!before.Contains(id)) return id;
        }

        return 0;
    }

    /// <summary>
    /// Storage the game keeps out of its cost pool; the neighbor's basket, which it does not: what the player staged
    /// there is going to her, so it is neither food to brew with nor a place for bottles; and every stove, whose pot food
    /// belongs to the dish it is cooking or about to cook.
    /// </summary>
    private static bool IsSetApart(long ownerId)
    {
        if (ItemManager.IsExcludedFromCostPool(ownerId)) return true;

        var furniture = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetAgent(ownerId)?.TryCast<Furniture>();

        return IsNeighborBasket(furniture) || IsStove(furniture);
    }

    private static bool IsStove(Furniture? furniture) => furniture != null && AgentTools.GetAgentComponent<CookingComponent>(furniture) != null;

    /// <summary>The basket's config id is 0 when there is none.</summary>
    private static bool IsNeighborBasket(Furniture? furniture)
    {
        var basketId = NeighborRules.GetBasketFurnitureId();

        return furniture != null && basketId > 0 && furniture.AgentConfigId == basketId;
    }

    /// <summary>One furniture by id, looked up directly rather than by walking every furniture in the base.</summary>
    private static Furniture? FurnitureOf(AgentManager? agents, long furnitureId) => agents?.GetAgent(furnitureId)?.TryCast<Furniture>();

    private static List<int> Ints(Il2CppSystem.Collections.Generic.List<int>? list)
    {
        var ints = new List<int>();

        for (var i = 0; list != null && i < list.Count; i++) ints.Add(list[i]);

        return ints;
    }

    private bool IsHome(AgentManager agents)
    {
        try
        {
            return agents.IsHomeMap(ExploreSearchStateManager.GetCurrentMapId());
        }
        catch (Exception ex)
        {
            ReportOnce("home check", ex);

            return false;
        }
    }

    private int PressCount(string operation, Func<int> press)
    {
        _fullReads.Forget();

        try
        {
            return press();
        }
        catch (Exception ex)
        {
            ReportOnce(operation, ex);

            return 0;
        }
    }

    private bool Press(string operation, Func<bool> press)
    {
        _fullReads.Forget();

        try
        {
            return press();
        }
        catch (Exception ex)
        {
            ReportOnce(operation, ex);

            return false;
        }
    }

    private static string ItemName(ConfigManager config, Config_Item item) =>
        Translate(config, item.ItemName) ?? NonEmpty(item.ItemName_Local) ?? NonEmpty(item.ItemName) ?? $"Item {item.ID}";

    private static string? IconName(Config_Item item) => NonEmpty(item.WebIcon) ?? NonEmpty(item.Icon);

    private static string? Translate(ConfigManager config, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var text = config.GetLocalTxt(key);

        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private void ReportOnce(string operation, Exception ex)
    {
        if (_reportedFailures.Add(operation)) _log($"{operation} failed (each kind reported once): {ex}");
    }

    /// <summary>An item row, null when the table has none, with whether the barrel takes it and whether it spoils.</summary>
    private readonly record struct Row(Config_Item? Item, bool CanBrew, bool Spoils);

    /// <summary>
    /// The game tables the catalog is read from. Each table is walked once, a row per step, in the table's own order;
    /// lookups by id go straight to the item table, so a missing id logs nothing.
    /// </summary>
    private sealed class Tables : ICatalogTables
    {
        private readonly ConfigManager _config;
        private readonly GameBrewing _owner;

        public Tables(ConfigManager config, Furniture? barrel, GameBrewing owner)
        {
            _config = config;
            _owner = owner;
            Drinks = Ints(barrel?.GetBrewProducts());
        }

        public IReadOnlyList<int> Drinks { get; }

        public IconIndex? Icons => _owner.IconsIfIn();

        public IEnumerable<IReadOnlyList<int>> RecipeResults()
        {
            var rows = _config._Config_CookingRecipe_Dict;

            if (rows == null) yield break;

            foreach (var row in rows.Values)
            {
                if (row != null) yield return new[] { row.FailItemID, row.NormalItemID, row.GoodItemID, row.PerfectItemID };
            }
        }

        /// <summary>Every food the brewing screen admits (Config_Item.CanBrew), with satiety and morale for a whole item.</summary>
        public IEnumerable<FoodRow?> Foods()
        {
            var rows = _config._Config_Item_Dict;

            if (rows == null) yield break;

            foreach (var row in rows.Values)
            {
                if (row == null || !row.CanBrew)
                {
                    yield return null;

                    continue;
                }

                var uses = Math.Max(row.UseTimes, 1);

                yield return new FoodRow(row.ID, ItemName(_config, row), IconName(row), Math.Max(row.ValueDisplay1, 0) * uses, row.ValueDisplay2 * uses, row.Life > 0 ? row.Life * 24.0 : null);
            }
        }

        public IEnumerable<(IReadOnlyList<int> Materials, IReadOnlyList<int> Products)> Productions()
        {
            var rows = _config._Config_ProductionList_Dict;

            if (rows == null) yield break;

            foreach (var row in rows.Values) yield return (Ints(row?.MaterialList), Ints(row?.ProductID));
        }

        public IEnumerable<IReadOnlyList<int>> Requests()
        {
            var rows = _config._Config_AidCommission_Dict;

            if (rows == null) yield break;

            foreach (var row in rows.Values) yield return Ints(row?.DemandItemIds);
        }

        public (string Name, string? IconName)? Item(int id) => ItemRow(id) is { } row ? (ItemName(_config, row), IconName(row)) : null;

        public string Description(int id) => ItemRow(id) is { } row ? Translate(_config, row.ItemDes1) ?? "" : "";

        private Config_Item? ItemRow(int id) => _config._Config_Item_Dict is { } table && table.TryGetValue(id, out var row) ? row : null;
    }

    /// <summary>
    /// Where a stack is kept, by the name the game gives that storage. Names are kept for the game and language they were
    /// read in; whether a storage is set apart is asked again on each read of every storage.
    /// </summary>
    private sealed class Places
    {
        private readonly ConfigManager _config;
        private readonly AgentManager _agents;
        private readonly long _roleId;
        private readonly bool _isChinese;
        private readonly Dictionary<long, string> _names = new();
        private readonly Dictionary<long, bool> _setApart = new();

        public Places(ConfigManager config, AgentManager agents, long roleId, bool isChinese)
        {
            _config = config;
            _agents = agents;
            _roleId = roleId;
            _isChinese = isChinese;
        }

        public bool IsFor(ConfigManager config, AgentManager agents, long roleId, bool isChinese) =>
            config.Pointer == _config.Pointer && agents.Pointer == _agents.Pointer && roleId == _roleId && isChinese == _isChinese;

        public string Name(long ownerId)
        {
            if (ownerId == _roleId) return Texts.Bag.Pick(_isChinese);
            if (_names.TryGetValue(ownerId, out var known)) return known;

            var name = FurnitureName(ownerId) ?? (Furniture.IsBrewBarrelOwner(ownerId) ? Texts.Barrel : Texts.Storage).Pick(_isChinese);

            _names[ownerId] = name;

            return name;
        }

        /// <summary>Asked for every stack the player can reach, and a few storages hold most of them, so each owner is looked up once per read.</summary>
        public bool IsSetApartOrBarrel(long ownerId)
        {
            if (_setApart.TryGetValue(ownerId, out var known)) return known;

            return _setApart[ownerId] = IsSetApart(ownerId) || Furniture.IsBrewBarrelOwner(ownerId);
        }

        public void ForgetSetApart() => _setApart.Clear();

        private string? FurnitureName(long ownerId)
        {
            var furniture = _agents.GetAgent(ownerId)?.TryCast<Furniture>();
            var rows = _config._Config_Furniture_Dict;

            if (furniture == null || rows == null || !rows.TryGetValue(furniture.AgentConfigId, out var row)) return null;

            return Translate(_config, row?.Name);
        }
    }
}
