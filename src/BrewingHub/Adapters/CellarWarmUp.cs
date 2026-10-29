using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BrewingHub.Core;

namespace BrewingHub.Adapters;

/// <summary>
/// The first brewing screen of a session used to compile the hub's code on the game's frame: reading the food,
/// planning each drink, and writing the catalog and the state for the page, most of a tenth of a second in one frame.
/// At load, on a worker, the plain part is run once on a made-up cellar and the rest is compiled without being run, so
/// nothing here touches the game. A failure only costs the first screen its head start, and is noted in the mod's file
/// only: the BepInEx console is left to the game's thread.
/// </summary>
internal static class CellarWarmUp
{
    private const string CoreNamespace = "BrewingHub.Core";

    private static readonly Type[] Adapters =
    {
        typeof(BrewSession), typeof(GameBrewing), typeof(BrewTicker), typeof(WebPage), typeof(PageMessages),
        typeof(StackingUnits), typeof(OptionalLive), typeof(WebIcons), typeof(WebKeyboard), typeof(GameLanguage), typeof(BrewLog),
    };

    public static void Start(Action<string> logFileOnly) =>
        Task.Run(() =>
        {
            try
            {
                BrewWarmUp.Run();

                foreach (var type in typeof(BrewWarmUp).Assembly.GetTypes())
                {
                    if (type.Namespace == CoreNamespace) Prepare(type);
                }

                foreach (var type in Adapters)
                {
                    Prepare(type);
                    PrepareNested(type);
                }
            }
            catch (Exception ex)
            {
                logFileOnly("warm-up failed, the first brewing screen may take a moment longer: " + ex.Message);
            }
        });

    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Compiles every method the type declares without running any. Nested types are listed on their own.</summary>
    private static void Prepare(Type type)
    {
        if (type.ContainsGenericParameters) return;

        foreach (var method in type.GetMethods(Declared))
        {
            if (!method.IsAbstract && !method.ContainsGenericParameters) RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }
    }

    /// <summary>An adapter's lambdas and records live in nested types, which the Core pass lists itself.</summary>
    private static void PrepareNested(Type type)
    {
        foreach (var nested in type.GetNestedTypes(Declared))
        {
            Prepare(nested);
            PrepareNested(nested);
        }
    }
}
