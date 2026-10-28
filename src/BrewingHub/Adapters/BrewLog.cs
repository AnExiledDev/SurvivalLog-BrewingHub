using System;
using System.IO;
using BepInEx.Logging;
using BrewingHub.Core;

namespace BrewingHub.Adapters;

/// <summary>
/// Writes to the BepInEx log and, while diagnostics are on, to an append-only file that survives restarts
/// (LogOutput.log is overwritten on the next launch).
/// </summary>
internal sealed class BrewLog
{
    private readonly ManualLogSource _bepinex;
    private readonly string _path;
    private readonly object _gate = new();
    private StreamWriter? _file;

    /// <summary>Whether lines also go to the file. Set from the Diagnostics setting.</summary>
    public bool IsFileOn { get; set; }

    public BrewLog(ManualLogSource bepinex, string path)
    {
        _bepinex = bepinex;
        _path = path;
    }

    /// <summary>Never throws: a logger that can fail would take down whatever it was asked to report on.</summary>
    public void Write(string message)
    {
        try
        {
            if (HookFailure.Is(message)) _bepinex.LogError(message);
            else _bepinex.LogInfo(message);
        }
        catch (Exception)
        {
            // The file copy below still has a chance.
        }

        WriteFileOnly(message);
    }

    /// <summary>The file alone, for a worker thread: the BepInEx console is left to the game's thread.</summary>
    public void WriteFileOnly(string message)
    {
        if (!IsFileOn) return;

        lock (_gate) AppendToFile($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
    }

    /// <summary>One writer stays open so a busy frame is not paying for an open and close per line.</summary>
    private void AppendToFile(string line)
    {
        try
        {
            _file ??= OpenFile();
            _file.WriteLine(line);
        }
        catch (Exception)
        {
            // The BepInEx log already has the line; the writer is dropped so the next line tries a fresh one.
            CloseFile();
        }
    }

    /// <summary>Shared for reading so the log can be tailed while the game runs; flushed per line so a crash loses nothing.</summary>
    private StreamWriter OpenFile() =>
        new(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };

    private void CloseFile()
    {
        try
        {
            _file?.Dispose();
        }
        catch (Exception)
        {
            // A writer that failed to write can fail to close too; it is being thrown away either way.
        }

        _file = null;
    }
}
