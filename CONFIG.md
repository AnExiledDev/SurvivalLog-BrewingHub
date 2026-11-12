# Brewing Hub settings

Settings live in `BepInEx\config\com.dev.survivallog.brewinghub.cfg`, created the first time the game starts with the mod. Edit it while the game is closed, or change the settings in game on the Mod Manager's Mods screen. Delete the file to get the defaults back.

## General

| Setting | Default | What it does |
| --- | --- | --- |
| `FillSpoilingFirst` | `true` | The food closest to spoiling goes in first. When `false`, the freshest food goes in first. Load always picks the barrel with the most bottles from the fewest foods and the least waste, so this only decides between foods that make an equally good barrel. Adding one food at a time, with a one-click button or a row on Ingredients, takes from the stack closest to spoiling, or the freshest. |
| `PutBottlesAway` | `false` | When you collect a barrel, every bottle of that drink in your bag moves to a home storage that already holds it. With no such storage, they stay in your bag. |

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `0` | How big Brewing Hub's own windows, panels and messages are drawn, in percent, from 75 to 200. At `0` they follow the UI size set in Exiled's Toolkit (its `UiScale`). |

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `LogToFile` | `false` | Also writes what Brewing Hub does to `BepInEx\brewing-hub.log`, which is kept between game starts. Turn it on when reporting a problem. |
