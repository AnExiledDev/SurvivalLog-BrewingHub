using System;
using System.Collections.Generic;

namespace BrewingHub.Core;

/// <summary>
/// The catalog for one game and language (the key), built a slice at a time. Asking for a key it has not built starts
/// the build and answers the catalog it had until the build ends, so a language change never holds up a frame. A
/// catalog built before any barrel stood has no drinks, so it is built once more when a barrel is asked for.
/// </summary>
public sealed class CatalogCache<TKey>
{
    private static readonly EqualityComparer<TKey> Same = EqualityComparer<TKey>.Default;

    private Catalog? _built;
    private TKey _builtKey = default!;
    private bool _builtWithDrinks;
    private CatalogBuild? _build;
    private SlicedJob? _job;
    private TKey _jobKey = default!;
    private bool _jobWithDrinks;

    public bool IsBuilding => _job != null;

    /// <summary>The catalog for this key when it is built and will do, else null; asks nothing of the caller.</summary>
    public Catalog? Built(TKey key, bool needsDrinks) =>
        _built != null && Same.Equals(_builtKey, key) && (_builtWithDrinks || !needsDrinks) ? _built : null;

    /// <summary>
    /// The catalog for this key when it is built; else starts building it (unless that build is under way) and answers
    /// the last catalog built, or null when none is.
    /// </summary>
    public Catalog? For(TKey key, bool needsDrinks, Func<CatalogBuild> start)
    {
        if (Built(key, needsDrinks) is { } built) return built;

        if (_job == null || !Same.Equals(_jobKey, key) || (needsDrinks && !_jobWithDrinks))
        {
            _build = start();
            _job = new SlicedJob(_build.Steps());
            _jobKey = key;
            _jobWithDrinks = needsDrinks;
        }

        return _built;
    }

    /// <summary>
    /// Runs the build until the slice is spent; the catalog on the call that finishes it, else null. A build that
    /// throws is dropped, so the catalog it was replacing stands and the next ask starts it again.
    /// </summary>
    public Catalog? Advance(Func<bool> isSliceSpent)
    {
        if (_job == null || _build == null) return null;

        bool isDone;

        try
        {
            isDone = _job.Advance(isSliceSpent);
        }
        catch
        {
            Drop();

            throw;
        }

        if (!isDone) return null;

        _built = _build.Result;
        _builtKey = _jobKey;
        _builtWithDrinks = _jobWithDrinks;
        _job = null;
        _build = null;

        return _built;
    }

    /// <summary>Stops the build under way (one that failed); the next ask starts it again.</summary>
    public void Drop()
    {
        _job = null;
        _build = null;
    }
}
