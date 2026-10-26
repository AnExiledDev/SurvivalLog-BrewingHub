namespace BrewingHub.Core;

/// <summary>
/// When the open brewing screen reads every home storage again, rather than only the bag and the open barrel. Storage
/// is read again every few seconds, when the game hour turns (food spoils by the hour), and after anything this mod
/// moved; in between, the storages as last read stand.
/// </summary>
public sealed class FullReadClock
{
    private readonly double _everySeconds;
    private double? _readAt;
    private int _readHour;

    public FullReadClock(double everySeconds = 3)
    {
        _everySeconds = everySeconds;
    }

    public bool IsDue(double now, int hour) => _readAt is not { } at || hour != _readHour || now < at || now - at >= _everySeconds;

    public void Read(double now, int hour)
    {
        _readAt = now;
        _readHour = hour;
    }

    /// <summary>The next look reads everything, as after a press.</summary>
    public void Forget() => _readAt = null;
}
