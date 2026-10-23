# Brewing Hub settings

Settings live in `BepInEx\config\com.dev.survivallog.brewinghub.cfg`, created the first time the game starts with the mod. Edit it while the game is closed, or change the settings in game on the Mod Manager's Mods screen. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start.

## General

| Setting | Default | What it does |
| --- | --- | --- |
| `FillSpoilingFirst` | `true` | Load and the one-click adds fill the barrel with the food closest to spoiling first. When `false`, they use the freshest food first. |
| `PutBottlesAway` | `false` | Bottles collected from a barrel move from your bag to a home storage that already holds that drink. With no such storage, they stay in your bag. |

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `LogToFile` | `false` | Also writes what Brewing Hub does to `BepInEx\brewing-hub.log`, which is kept between game starts. Turn it on when reporting a problem. |
