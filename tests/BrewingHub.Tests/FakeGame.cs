using BrewingHub.Core;

namespace BrewingHub.Tests;

/// <summary>A base with a bag, a chest and brewing barrels, kept in plain lists so presses can be checked by what moved.</summary>
internal sealed class FakeGame : IBrewGame
{
    public const long Role = 1;
    public const long Chest = 50;

    /// <summary>Past 2^53, as real furniture ids are.</summary>
    public const long BarrelA = 9007199254740993;

    public const long BarrelB = 77;

    public const int Berry = 101;
    public const int Mushroom = 102;
    public const int Potato = 103;
    public const int Stew = 104;
    public const int Ale = 201;
    public const int Beer = 202;
    public const int Wine = 203;

    public static readonly Catalog Book = new(
        new Dictionary<int, Ingredient>
        {
            [Berry] = new(Berry, "Berry", "Res/Icon/berry.png", 10, 6, false, 72),
            [Mushroom] = new(Mushroom, "Mushroom", "Res/Icon/mushroom.png", 10, 2, false, 48),
            [Potato] = new(Potato, "Potato", null, 20, -4, false, null),
            [Stew] = new(Stew, "Stew", null, 30, 15, true, 24),
        },
        new[]
        {
            new Drink(0, Ale, "Ale", null, "Thin and sour.", Array.Empty<Use>(), 0),
            new Drink(1, Beer, "Beer", null, "A fair drink.", new[] { new Use(301, new[] { 302 }) }, 2),
            new Drink(2, Wine, "Wine", "Res/Icon/wine.png", "Strong and sweet.", Array.Empty<Use>(), 0),
        },
        new Dictionary<int, (string, string?)> { [Ale] = ("Ale", null), [Beer] = ("Beer", null), [Wine] = ("Wine", "Res/Icon/wine.png"), [301] = ("Punch", null), [302] = ("Sugar", null) });

    private long _nextInstance = 9000;

    public Rules Rules { get; set; } = Rules.Shipped;

    public bool HasGame { get; set; } = true;

    public bool CanSplit { get; set; } = true;

    public bool RefusesMoves { get; set; }

    public bool RefusesStart { get; set; }

    public string? CollectBlocked { get; set; }

    public long? OpenId { get; set; } = BarrelA;

    public List<Stack> Stock { get; } = new();

    public List<FakeBarrel> Barrels { get; } = new() { new FakeBarrel(BarrelA, "Brewing Barrel"), new FakeBarrel(BarrelB, "Cellar Barrel") };

    public List<long> ChangedIds { get; } = new();

    public List<long> Started { get; } = new();

    public FakeBarrel A => Barrels[0];

    public static Stack S(long instance, int item, int count = 1, long owner = Role, double? hours = null)
    {
        var food = Book.Ingredients[item];

        return new Stack(instance, item, count, owner, PlaceOf(owner), hours, food.Sat, food.Mood);
    }

    public Cellar Cellar()
    {
        var open = Barrels.FirstOrDefault(barrel => barrel.Id == OpenId);
        var mix = Mix.None;

        foreach (var stack in open?.Contents ?? new List<Stack>()) mix = mix.Add(stack, stack.Count);

        return new Cellar(Role, Rules, Book, Stock.ToList(), Barrels.Select(barrel => barrel.ToBarrel()).ToList(), OpenId, mix, CanSplit);
    }

    public BrewRead Read() => HasGame ? new BrewRead.Ready(Cellar()) : new BrewRead.NoGame();

    public int MoveUnits(long instanceId, long fromOwnerId, long toOwnerId, int units)
    {
        var source = ListOf(fromOwnerId);
        var stack = source.FirstOrDefault(s => s.InstanceId == instanceId && s.OwnerId == fromOwnerId);

        if (RefusesMoves || stack == null || units <= 0) return 0;

        var n = CanSplit ? Math.Min(units, stack.Count) : stack.Count;
        var index = source.IndexOf(stack);

        if (n >= stack.Count) source.RemoveAt(index);
        else source[index] = stack with { Count = stack.Count - n };

        var target = ListOf(toOwnerId);
        var same = target.FindIndex(s => s.OwnerId == toOwnerId && s.ItemId == stack.ItemId && s.HoursLeft == stack.HoursLeft);

        if (same >= 0) target[same] = target[same] with { Count = target[same].Count + n };
        else target.Add(stack with { InstanceId = _nextInstance++, Count = n, OwnerId = toOwnerId, Place = PlaceOf(toOwnerId) });

        return n;
    }

    public bool MoveStack(long instanceId, long fromOwnerId, long toOwnerId)
    {
        var stack = ListOf(fromOwnerId).FirstOrDefault(s => s.InstanceId == instanceId);

        return stack != null && MoveUnits(instanceId, fromOwnerId, toOwnerId, stack.Count) == stack.Count;
    }

    public bool Start(long furnitureId)
    {
        if (RefusesStart) return false;

        var barrel = Barrels.First(b => b.Id == furnitureId);

        barrel.State = BarrelState.Brewing;
        barrel.Remain = barrel.Total = 72;
        Started.Add(furnitureId);

        return true;
    }

    public Collected Collect(long furnitureId)
    {
        if (CollectBlocked != null) return new Collected(0, CollectBlocked);

        var barrel = Barrels.First(b => b.Id == furnitureId);
        var bottles = barrel.ResultCount;

        barrel.State = BarrelState.Idle;
        barrel.ResultCount = 0;
        barrel.Contents.Clear();

        return new Collected(bottles, null);
    }

    public void Changed(long furnitureId) => ChangedIds.Add(furnitureId);

    private List<Stack> ListOf(long ownerId) => Barrels.FirstOrDefault(b => b.Id == ownerId)?.Contents ?? Stock;

    private static string PlaceOf(long owner) => owner switch
    {
        Role => "Bag",
        Chest => "Chest",
        _ => "Brewing Barrel",
    };
}

internal sealed class FakeBarrel
{
    public FakeBarrel(long id, string name)
    {
        Id = id;
        Name = name;
    }

    public long Id { get; }

    public string Name { get; }

    public BarrelState State { get; set; }

    public bool IsLocked { get; set; }

    public int ResultItemId { get; set; }

    public int ResultCount { get; set; }

    public int Remain { get; set; }

    public int Total { get; set; }

    public List<Stack> Contents { get; } = new();

    /// <summary>Finished with this many bottles of a drink.</summary>
    public FakeBarrel Done(int drink, int bottles)
    {
        State = BarrelState.Done;
        ResultItemId = drink;
        ResultCount = bottles;

        return this;
    }

    public Barrel ToBarrel() => new(Id, Name, State, IsLocked, ResultItemId, ResultCount, Remain, Total, Contents.ToList());
}
