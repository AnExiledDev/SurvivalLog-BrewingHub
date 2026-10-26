# Brewing Hub

Know what a barrel will brew before you start it. Brewing Hub adds a fourth column to the game's brewing barrel screen with the best barrel of each drink you can make from your bag and home storage, the drinks and what they are for, every brewable food's numbers, and every barrel in your base. As you fill a barrel it shows the quality, the drink, the bottles and the brewing time, with one-click changes that make a better drink or one more bottle.

## Features

- **Brew now**: the best barrel of Strong, Average and Poor drink your food can make, with how many bottles, the quality, the brewing time and where each food comes from (food that spoils within a day is marked). Load puts what is in the barrel back in your bag, then fills it from your bag and storages. Load uses the food closest to spoiling first, or the freshest first if you prefer (setting `FillSpoilingFirst`). Spoiled food is never used.
- **Quality as you fill**: under the barrel, the mix's quality (total morale over total satiety), the line for the next drink up, the satiety that fills no bottle and how much more fills the next one. The game's three drink names show the quality each one starts at.
- **One-click changes**: above the start button, buttons to add a food from your bag or storage, or take one out, when that makes a better drink or one more bottle, with what the barrel would make after.
- **Recipes**: each drink with its quality range, its description, the foods best for it, what it is used in and how many relief requests ask for it. Your talents' effect on brewing time and satiety per bottle is listed too.
- **Ingredients**: every brewable food with its satiety, morale, quality, how many it takes for one bottle, how many you have, and how many more lift the barrel to the next drink. Search, filter by raw food or food you have, and sort by satiety, quality or how many you have. Click a row to put one in the barrel.
- **Barrels**: every brewing barrel in your base, what it is brewing, the time left, and a Collect button on finished ones.
- **Collect and brew again**: on a finished barrel, one button takes the bottles and fills and starts it again for the same drink. A "Brew again when collected" switch does that on its own whenever the barrel's bottles are taken, by any means. The switch lasts until you leave the game.
- **Put bottles away** (setting `PutBottlesAway`, off by default): when you collect a barrel, every bottle of that drink in your bag moves to a home storage that already holds it.
- With Exiled's Toolkit, a barrel finishing and a barrel brewing again show as toasts.

Brewing Hub adds nothing to your save and does not edit game files. You can remove it at any time.

## Requirements

- Survival Log 1.1.19611. Built for this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- Optional: [Stacking](https://www.nexusmods.com/survivallog/mods/74). Brewing Hub moves food Stacking made stackable one unit at a time, the way Stacking wants it. Without Stacking, nothing changes.
- Optional: [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game, and barrel notices show as toasts. Without it, settings take effect at the next game start.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Optional, to change settings without a restart: install Exiled's Toolkit. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/61), and extract it into the game folder (the folder with `SurvivalLog.exe`).
3. Download `BrewingHub-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-BrewingHub/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/93).
4. Extract the zip into the game folder. It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\BrewingHub.dll`

## Uninstall

Delete `BrewingHub.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.brewinghub.cfg` and, if it exists, `BepInEx\brewing-hub.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.brewinghub.cfg`, created the first time the game starts with the mod. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Brewing Hub 0.1.3 started`. A line starting with `could not` means part of the mod does not work on this game version.

## Known issues

- The "Brew again when collected" switch is not saved: it turns off when you leave the game.
- The Barrels tab can collect a finished barrel but cannot fill or start one; open that barrel to do so.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . When reporting a bug, attach `BepInEx\LogOutput.log` and, if you turned on `LogToFile`, `BepInEx\brewing-hub.log`.

To reach me directly, message `exiled.dev` on Discord; you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

Check out my [Survival Log wiki](https://sldb.core-directive.com/en/) for items, crafting, maps and more.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/BrewingHub -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. Compiling also needs Exiled's Toolkit 0.4.0 or newer: point `-p:ToolkitDll="C:\path\to\ExiledToolkit.dll"` at its DLL. Players don't need the Toolkit to run the mod. The output DLL is at `src\BrewingHub\bin\Release\BrewingHub.dll`.

The unit tests cover the brewing math, the planner and the page script, and need nothing from the game:

```
dotnet test tests/BrewingHub.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
