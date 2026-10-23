using System;
using System.Collections.Generic;
using System.Linq;
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
    private const int DishIdsFrom = 50000;
    private const string SpeedTalent = "Buff/AE_BrewSpeedUp";

    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();

    private IconIndex? _icons;
    private (IntPtr Config, bool IsChinese) _catalogKey;
    private Catalog? _catalog;

    public GameBrewing(Action<string> log)
    {
        _log = log;
    }

    public BrewRead Read()
    {
        try
        {
            return ReadCellar();
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
                if (QuickMove(items, current, fromOwnerId, toOwnerId)) moved += count;

                break;
            }

            if (one == false)
            {
                _log($"stacking did not move a unit of stack {current} from {fromOwnerId} to {toOwnerId}");

                break;
            }

            moved++;
            current = IdsOf(items, fromOwnerId, configId).FirstOrDefault(id => !before.Contains(id));
        }

        if (moved > 0) Ac_Brew_RefreshBag.SendAction();

        return moved;
    });

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
    /// Bottles in the bag go to a home storage that already holds that drink, so a collected barrel ends up beside the
    /// earlier ones. Returns the place they went, or null when none holds it or the game refused.
    /// </summary>
    public string? PutAway(int drinkId)
    {
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

            var isMoved = false;
            var held = items.GetItemDataList(role.InstanceId);
            var ids = new List<long>();

            for (var i = 0; held != null && i < held.Count; i++)
            {
                if (held[i] is { } item && item.ItemConfigId == drinkId) ids.Add(item.InstanceId);
            }

            foreach (var id in ids) isMoved |= items.TryQuickMoveItem(id, role.InstanceId, to, true);

            if (!isMoved) return null;

            Ac_Brew_RefreshBag.SendAction();

            return new Places(config, agents, role.InstanceId, GameLanguage.IsChinese()).Name(to);
        }
        catch (Exception ex)
        {
            ReportOnce("put away", ex);

            return null;
        }
    }

    /// <summary>True while the game's brewing screen is up. Cheap enough for every frame.</summary>
    public bool IsBrewScreenOpen()
    {
        try
        {
            return GameSingleton.Existing<ReduxUISystem>() is { } ui && ui.IsUIActive(Il2CppType.Of<WebUI_Brew>());
        }
        catch (Exception ex)
        {
            ReportOnce("brewing screen check", ex);

            return false;
        }
    }

    private BrewRead ReadCellar()
    {
        var game = LoadedGame();

        if (game == null) return new BrewRead.NoGame();

        var (world, config, agents, role) = game.Value;
        var isChinese = GameLanguage.IsChinese();

        if (!IsHome(agents)) return new BrewRead.NotReady(Texts.Away.Pick(isChinese));

        var items = world._ItemManager;

        if (items == null) return new BrewRead.NotReady(Texts.ReadFailed.Pick(isChinese));

        var hour = world._GameTimeManager?.GetHour() ?? 0;
        var places = new Places(config, agents, role.InstanceId, isChinese);
        var furnitures = BarrelFurnitures(agents);
        var catalog = Catalog(config, furnitures.FirstOrDefault(), isChinese);
        var stock = ReadStock(items, config, role.InstanceId, hour, places);
        var nowHour = Furniture.GetBrewNowHour();
        var barrels = furnitures.Select(furniture => ReadBarrel(items, config, furniture, hour, nowHour, places)).ToList();
        var openId = OpenBarrelId();
        var open = openId is { } id ? furnitures.FirstOrDefault(furniture => furniture.InstanceId == id) : null;

        return new BrewRead.Ready(new Cellar(role.InstanceId, ReadRules(config), catalog, stock, barrels, open?.InstanceId, open == null ? Mix.None : Preview(open), StackingUnits.IsPresent));
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

    private static List<Furniture> BarrelFurnitures(AgentManager agents)
    {
        var barrels = new List<Furniture>();
        var furnitures = agents.GetAllFurnitures();

        for (var i = 0; furnitures != null && i < furnitures.Count; i++)
        {
            if (furnitures[i] is { } furniture && furniture.IsBrewFurniture()) barrels.Add(furniture);
        }

        return barrels;
    }

    /// <summary>The barrel whose brewing screen is open, from the screen's own state, or null.</summary>
    private static long? OpenBarrelId()
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screen = Il2CppType.Of<WebUI_Brew>();

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

    private static Barrel ReadBarrel(ItemManager items, ConfigManager config, Furniture furniture, int hour, int nowHour, Places places)
    {
        var brew = AgentTools.GetAgentComponent<BrewComponent>(furniture);
        var contents = new List<Core.Stack>();
        var held = items.GetItemDataList(furniture.InstanceId);
        var name = places.Name(furniture.InstanceId);

        for (var i = 0; held != null && i < held.Count; i++)
        {
            if (held[i] is { ItemCount: > 0 } item && config.Get_Config_Item(item.ItemConfigId) is { CanBrew: true } row) contents.Add(StackOf(item, row, hour, name));
        }

        var state = brew == null ? BarrelState.Idle : (BarrelState)Math.Clamp(brew.State, 0, 2);

        return new Barrel(
            furniture.InstanceId,
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
    /// skipping storage the game keeps out of that pool and anything already in a barrel.
    /// </summary>
    private static List<Core.Stack> ReadStock(ItemManager items, ConfigManager config, long roleId, int hour, Places places)
    {
        var stock = new List<Core.Stack>();
        var available = items.GetAllAvailableItems(roleId);

        for (var i = 0; available != null && i < available.Count; i++)
        {
            var item = available[i];

            if (item == null || item.ItemCount <= 0) continue;
            if (item.OwnerId != roleId && (IsSetApart(item.OwnerId) || Furniture.IsBrewBarrelOwner(item.OwnerId))) continue;
            if (config.Get_Config_Item(item.ItemConfigId) is not { CanBrew: true } row) continue;

            stock.Add(StackOf(item, row, hour, places.Name(item.OwnerId)));
        }

        return stock;
    }

    /// <summary>
    /// One stack, each unit giving what Furniture.CalcBrewPreview counts: the item's satiety (never below 0) and
    /// morale, each times the uses it has left (a part-used item gives less).
    /// </summary>
    private static Core.Stack StackOf(ItemData item, Config_Item row, int hour, string place)
    {
        var uses = Furniture.GetBrewUseTimes(item, row);
        var sat = Math.Max(0, ItemValueDisplayHelper.GetEffectiveVD(item, row, SatietyValue)) * uses;
        var mood = ItemValueDisplayHelper.GetEffectiveVD(item, row, MoraleValue) * uses;
        var hoursLeft = row.Life > 0 ? item.StartTime + item.TimeLeft - hour : (double?)null;

        return new Core.Stack(item.InstanceId, item.ItemConfigId, item.ItemCount, item.OwnerId, place, hoursLeft, sat, mood);
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
    /// The tables only change with the language, so the catalog is built once per game and language. The drinks come
    /// from a barrel, so a catalog built before the first barrel stood is built again once one does.
    /// </summary>
    private Catalog Catalog(ConfigManager config, Furniture? barrel, bool isChinese)
    {
        var key = (config.Pointer, isChinese);

        if (_catalog != null && _catalogKey == key && (_catalog.Drinks.Count > 0 || barrel == null)) return _catalog;

        var items = new Dictionary<int, (string Name, string? Icon)>();
        var drinks = ReadDrinks(config, barrel, items);

        _catalog = new Catalog(ReadIngredients(config), drinks, items);
        _catalogKey = key;
        _log($"read {_catalog.Ingredients.Count} brewable foods and {drinks.Count} drinks");

        return _catalog;
    }

    /// <summary>
    /// Every food the brewing screen admits (Config_Item.CanBrew), with satiety and morale for a whole item. A dish is
    /// what a cooking recipe makes.
    /// </summary>
    private Dictionary<int, Ingredient> ReadIngredients(ConfigManager config)
    {
        var ingredients = new Dictionary<int, Ingredient>();
        var rows = config._Config_Item_Dict;
        var dishes = Dishes(config);

        if (rows == null) return ingredients;

        foreach (var row in rows.Values)
        {
            if (row == null || !row.CanBrew) continue;

            var uses = Math.Max(row.UseTimes, 1);
            var isDish = dishes.Contains(row.ID) || row.ID >= DishIdsFrom;

            ingredients[row.ID] = new Ingredient(row.ID, ItemName(config, row), ItemIcon(row), Math.Max(row.ValueDisplay1, 0) * uses, row.ValueDisplay2 * uses, isDish, row.Life > 0 ? row.Life * 24.0 : null);
        }

        return ingredients;
    }

    private static HashSet<int> Dishes(ConfigManager config)
    {
        var dishes = new HashSet<int>();
        var rows = config._Config_CookingRecipe_Dict;

        if (rows == null) return dishes;

        foreach (var row in rows.Values)
        {
            if (row == null) continue;

            dishes.Add(row.FailItemID);
            dishes.Add(row.NormalItemID);
            dishes.Add(row.GoodItemID);
            dishes.Add(row.PerfectItemID);
        }

        return dishes;
    }

    /// <summary>
    /// The barrel's drinks by tier (Furniture.GetBrewProducts), each with the workbench recipes that take it and the
    /// relief requests that ask for it. Names and icons of everything those name go into items.
    /// </summary>
    private List<Drink> ReadDrinks(ConfigManager config, Furniture? barrel, Dictionary<int, (string Name, string? Icon)> items)
    {
        var drinks = new List<Drink>();
        var products = barrel?.GetBrewProducts();

        for (var tier = 0; products != null && tier < products.Count; tier++)
        {
            var id = products[tier];
            var row = config.Get_Config_Item(id);
            var uses = UsesOf(config, id);

            Name(config, items, id);

            foreach (var use in uses)
            {
                Name(config, items, use.MakesId);

                foreach (var with in use.WithIds) Name(config, items, with);
            }

            var description = row == null ? "" : Translate(config, row.ItemDes1) ?? "";

            drinks.Add(new Drink(tier, id, items[id].Name, items[id].Icon, description, uses, AsksFor(config, id)));
        }

        return drinks;
    }

    private static List<Use> UsesOf(ConfigManager config, int drinkId)
    {
        var uses = new List<Use>();
        var rows = config._Config_ProductionList_Dict;

        if (rows == null) return uses;

        foreach (var row in rows.Values)
        {
            var materials = Ints(row?.MaterialList);
            var products = Ints(row?.ProductID);

            if (products.Count > 0 && materials.Contains(drinkId)) uses.Add(new Use(products[0], materials.Where(id => id != drinkId).ToList()));
        }

        return uses;
    }

    private static int AsksFor(ConfigManager config, int drinkId)
    {
        var rows = config._Config_AidCommission_Dict;
        var asks = 0;

        if (rows == null) return 0;

        foreach (var row in rows.Values)
        {
            if (Ints(row?.DemandItemIds).Contains(drinkId)) asks++;
        }

        return asks;
    }

    private void Name(ConfigManager config, Dictionary<int, (string Name, string? Icon)> items, int id)
    {
        if (items.ContainsKey(id)) return;

        var row = config.Get_Config_Item(id);

        items[id] = row == null ? ($"Item {id}", null) : (ItemName(config, row), ItemIcon(row));
    }

    /// <summary>ItemManager.TryQuickMoveItem, what the brewing screen's shift-click ends in, without its messages.</summary>
    private bool QuickMove(ItemManager items, long instanceId, long fromOwnerId, long toOwnerId)
    {
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

    /// <summary>
    /// Storage the game keeps out of its cost pool, and the neighbor's basket, which it does not: what the player
    /// staged there is going to her, so it is neither food to brew with nor a place for bottles.
    /// </summary>
    private static bool IsSetApart(long ownerId) =>
        ItemManager.IsExcludedFromCostPool(ownerId) || IsNeighborBasket(GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetAgent(ownerId)?.TryCast<Furniture>());

    /// <summary>The basket's config id is 0 when there is none.</summary>
    private static bool IsNeighborBasket(Furniture? furniture)
    {
        var basketId = NeighborRules.GetBasketFurnitureId();

        return furniture != null && basketId > 0 && furniture.AgentConfigId == basketId;
    }

    private static Furniture? FurnitureOf(AgentManager? agents, long furnitureId)
    {
        var furnitures = agents?.GetAllFurnitures();

        for (var i = 0; furnitures != null && i < furnitures.Count; i++)
        {
            if (furnitures[i]?.InstanceId == furnitureId) return furnitures[i];
        }

        return null;
    }

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

    private string? ItemIcon(Config_Item item) => Icons().Resolve(NonEmpty(item.WebIcon) ?? NonEmpty(item.Icon));

    private IconIndex Icons() => _icons ??= WebIcons.Load(_log);

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

    /// <summary>Where a stack is kept, by the name the game gives that storage. Looked up once per owner per read.</summary>
    private sealed class Places
    {
        private readonly ConfigManager _config;
        private readonly AgentManager _agents;
        private readonly long _roleId;
        private readonly bool _isChinese;
        private readonly Dictionary<long, string> _names = new();

        public Places(ConfigManager config, AgentManager agents, long roleId, bool isChinese)
        {
            _config = config;
            _agents = agents;
            _roleId = roleId;
            _isChinese = isChinese;
        }

        public string Name(long ownerId)
        {
            if (ownerId == _roleId) return Texts.Bag.Pick(_isChinese);
            if (_names.TryGetValue(ownerId, out var known)) return known;

            var name = FurnitureName(ownerId) ?? (Furniture.IsBrewBarrelOwner(ownerId) ? Texts.Barrel : Texts.Storage).Pick(_isChinese);

            _names[ownerId] = name;

            return name;
        }

        private string? FurnitureName(long ownerId)
        {
            var furnitures = _agents.GetAllFurnitures();
            var rows = _config._Config_Furniture_Dict;

            for (var i = 0; furnitures != null && rows != null && i < furnitures.Count; i++)
            {
                var furniture = furnitures[i];

                if (furniture?.InstanceId != ownerId || !rows.ContainsKey(furniture.AgentConfigId)) continue;

                return Translate(_config, rows[furniture.AgentConfigId]?.Name);
            }

            return null;
        }
    }
}
