using System.Collections.Generic;
using System.Linq;

namespace BrewingHub.Core;

/// <summary>What collecting a barrel gave: the bottles that went to the bag, or the game's reason it would not (null when it did).</summary>
public sealed record Collected(int Bottles, string? Blocked);

/// <summary>The game's side of every read and press, so the press logic can be tested without the game.</summary>
public interface IBrewGame
{
    BrewRead Read();

    /// <summary>Moves up to this many units of a stack; with no way to split it, the whole stack. Returns the units moved.</summary>
    int MoveUnits(long instanceId, long fromOwnerId, long toOwnerId, int units);

    bool MoveStack(long instanceId, long fromOwnerId, long toOwnerId);

    /// <summary>Starts the barrel brewing what is in it. False when the game refused.</summary>
    bool Start(long furnitureId);

    Collected Collect(long furnitureId);

    /// <summary>Tells the game what is in a barrel changed, so its screen and its numbers follow.</summary>
    void Changed(long furnitureId);
}

/// <summary>The presses: fill a barrel for a drink, add or take out one unit, empty it, collect, collect and brew again.</summary>
public static class BrewRun
{
    public static Pressed Load(IBrewGame game, long furnitureId, int tier, bool isSpoilingFirst, bool isChinese)
    {
        if (!TryIdle(game, furnitureId, isChinese, out var cellar, out var barrel, out var refusal)) return refusal;

        var drink = DrinkName(cellar, tier, isChinese);
        var pool = cellar.Stock.Concat(barrel.Contents);

        if (Planner.For(cellar.Rules, pool, tier, isSpoilingFirst, cellar.CanSplit) == null) return new Pressed(false, Texts.NotEnough.Say(isChinese, drink));

        if (barrel.Contents.Count > 0)
        {
            if (!EmptyAll(game, cellar.RoleId, barrel)) return new Pressed(false, Texts.EmptyRefused.Pick(isChinese));
            if (game.Read() is not BrewRead.Ready { Cellar: var again }) return new Pressed(false, Texts.NoGame.Pick(isChinese));

            cellar = again;
        }

        var plan = Planner.For(cellar.Rules, cellar.Stock, tier, isSpoilingFirst, cellar.CanSplit);

        if (plan == null) return new Pressed(false, Texts.NotEnough.Say(isChinese, drink));

        var moved = plan.Takes.Sum(take => game.MoveUnits(take.Stack.InstanceId, take.Stack.OwnerId, furnitureId, take.Units));

        game.Changed(furnitureId);

        if (moved >= plan.Units) return new Pressed(true, Texts.Filled.Say(isChinese, drink));

        return new Pressed(false, Texts.MovedSome.Say(isChinese, moved, plan.Units));
    }

    /// <summary>One unit of a food into the open barrel, from the stack closest to spoiling (or the freshest).</summary>
    public static Pressed Add(IBrewGame game, int itemId, bool isSpoilingFirst, bool isChinese)
    {
        if (!TryOpenIdle(game, isChinese, out var cellar, out var barrel, out var refusal)) return refusal;

        var name = ItemName(cellar, itemId);
        var stack = Planner.InOrder(cellar.Stock.Where(stack => stack.ItemId == itemId && !stack.IsSpoiled && stack.Count > 0), isSpoilingFirst).FirstOrDefault();

        if (stack == null) return new Pressed(false, Texts.NoneLeft.Say(isChinese, name));
        if (game.MoveUnits(stack.InstanceId, stack.OwnerId, barrel.FurnitureId, 1) <= 0) return new Pressed(false, Texts.MoveRefused.Pick(isChinese));

        game.Changed(barrel.FurnitureId);

        return new Pressed(true, Texts.Added.Say(isChinese, name, stack.Place));
    }

    /// <summary>One unit of a food out of the open barrel, to the bag.</summary>
    public static Pressed Remove(IBrewGame game, int itemId, bool isChinese)
    {
        if (!TryOpenIdle(game, isChinese, out var cellar, out var barrel, out var refusal)) return refusal;

        var name = ItemName(cellar, itemId);
        var stack = barrel.Contents.FirstOrDefault(stack => stack.ItemId == itemId && stack.Count > 0);

        if (stack == null) return new Pressed(false, Texts.NotInBarrel.Say(isChinese, name));
        if (game.MoveUnits(stack.InstanceId, barrel.FurnitureId, cellar.RoleId, 1) <= 0) return new Pressed(false, Texts.MoveRefused.Pick(isChinese));

        game.Changed(barrel.FurnitureId);

        return new Pressed(true, Texts.Removed.Say(isChinese, name));
    }

    /// <summary>Everything in the open barrel to the bag.</summary>
    public static Pressed Empty(IBrewGame game, bool isChinese)
    {
        if (!TryOpenIdle(game, isChinese, out var cellar, out var barrel, out var refusal)) return refusal;
        if (barrel.Contents.Count == 0) return new Pressed(false, Texts.AlreadyEmpty.Pick(isChinese));

        var isEmptied = EmptyAll(game, cellar.RoleId, barrel);

        game.Changed(barrel.FurnitureId);

        return isEmptied ? new Pressed(true, Texts.Emptied.Pick(isChinese)) : new Pressed(false, Texts.EmptyRefused.Pick(isChinese));
    }

    /// <summary>Takes a finished barrel's bottles to the bag, by the game's own harvest.</summary>
    public static Pressed Collect(IBrewGame game, long furnitureId, bool isChinese)
    {
        if (game.Read() is not BrewRead.Ready { Cellar: var cellar }) return new Pressed(false, Texts.NoGame.Pick(isChinese));
        if (cellar.BarrelOf(furnitureId) is not { } barrel) return new Pressed(false, Texts.NoBarrel.Pick(isChinese));
        if (barrel.State != BarrelState.Done) return new Pressed(false, Texts.NotDone.Pick(isChinese));

        var collected = game.Collect(furnitureId);

        if (collected.Blocked != null) return new Pressed(false, collected.Blocked);
        if (collected.Bottles <= 0) return new Pressed(false, Texts.CollectRefused.Pick(isChinese));

        return new Pressed(true, Texts.Collected.Say(isChinese, collected.Bottles, ItemName(cellar, barrel.ResultItemId)));
    }

    /// <summary>Collects, fills the barrel again for the same drink, and starts it. Stops at the first step that fails, saying which.</summary>
    public static Pressed CollectAndBrew(IBrewGame game, long furnitureId, int tier, bool isSpoilingFirst, bool isChinese)
    {
        var collected = Collect(game, furnitureId, isChinese);

        return collected.IsDone ? LoadAndStart(game, furnitureId, tier, isSpoilingFirst, isChinese, collected.Message) : collected;
    }

    /// <summary>Fills an idle barrel for the drink and starts it; the message leads with what came before, when anything did.</summary>
    public static Pressed LoadAndStart(IBrewGame game, long furnitureId, int tier, bool isSpoilingFirst, bool isChinese, string before = "")
    {
        var loaded = Load(game, furnitureId, tier, isSpoilingFirst, isChinese);
        var lead = before.Length > 0 ? before + " " : "";

        if (!loaded.IsDone) return new Pressed(false, lead + loaded.Message);
        if (!game.Start(furnitureId)) return new Pressed(false, lead + Texts.NotStarted.Pick(isChinese));

        game.Changed(furnitureId);

        return new Pressed(true, lead + Texts.Started.Say(isChinese, DrinkNameOf(game, tier, isChinese)));
    }

    private static bool EmptyAll(IBrewGame game, long roleId, Barrel barrel)
    {
        var isEmptied = true;

        foreach (var stack in barrel.Contents) isEmptied &= game.MoveStack(stack.InstanceId, barrel.FurnitureId, roleId);

        return isEmptied;
    }

    private static bool TryOpenIdle(IBrewGame game, bool isChinese, out Cellar cellar, out Barrel barrel, out Pressed refusal)
    {
        cellar = null!;
        barrel = null!;

        if (game.Read() is not BrewRead.Ready { Cellar: var read })
        {
            refusal = new Pressed(false, Texts.NoGame.Pick(isChinese));

            return false;
        }

        if (read.Open is not { } open)
        {
            refusal = new Pressed(false, Texts.NoBarrel.Pick(isChinese));

            return false;
        }

        return IsIdle(read, open, isChinese, out cellar, out barrel, out refusal);
    }

    private static bool TryIdle(IBrewGame game, long furnitureId, bool isChinese, out Cellar cellar, out Barrel barrel, out Pressed refusal)
    {
        cellar = null!;
        barrel = null!;

        if (game.Read() is not BrewRead.Ready { Cellar: var read })
        {
            refusal = new Pressed(false, Texts.NoGame.Pick(isChinese));

            return false;
        }

        if (read.BarrelOf(furnitureId) is not { } found)
        {
            refusal = new Pressed(false, Texts.NoBarrel.Pick(isChinese));

            return false;
        }

        return IsIdle(read, found, isChinese, out cellar, out barrel, out refusal);
    }

    private static bool IsIdle(Cellar read, Barrel found, bool isChinese, out Cellar cellar, out Barrel barrel, out Pressed refusal)
    {
        cellar = read;
        barrel = found;
        refusal = new Pressed(false, "");

        if (found.State == BarrelState.Done) refusal = new Pressed(false, Texts.CollectFirst.Pick(isChinese));
        else if (found.State == BarrelState.Brewing || found.IsLocked) refusal = new Pressed(false, Texts.Busy.Pick(isChinese));
        else return true;

        return false;
    }

    private static string DrinkName(Cellar cellar, int tier, bool isChinese) => cellar.Catalog.DrinkOf(tier)?.Name ?? Texts.Drink.Pick(isChinese);

    private static string DrinkNameOf(IBrewGame game, int tier, bool isChinese) =>
        game.Read() is BrewRead.Ready { Cellar: var cellar } ? DrinkName(cellar, tier, isChinese) : Texts.Drink.Pick(isChinese);

    private static string ItemName(Cellar cellar, int itemId)
    {
        if (cellar.Catalog.Ingredients.TryGetValue(itemId, out var ingredient)) return ingredient.Name;

        return cellar.Catalog.Items.TryGetValue(itemId, out var item) ? item.Name : $"#{itemId}";
    }
}
