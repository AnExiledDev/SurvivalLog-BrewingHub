# Changelog

## 0.3.2

- The search box on the Ingredients tab no longer hides every food when a space is typed before or after the name.
- Made for Survival Log 1.1.20719.

## 0.3.1

- Clearer wording in the settings and on screen.
- A barrel that makes one bottle now says "1 bottle", not "1 bottles".
- Made for Survival Log 1.1.20719.

## 0.3.0

- Brew now suggests barrels with far fewer foods in them. Each barrel makes the most bottles it can from as few foods as possible, so when you have Watermelons, a few of them can be the whole barrel instead of a long mix of everything.
- Brew now wastes less food. It no longer adds food that doesn't make another bottle.
- The same food in one storage now shows as one line on Brew now, however many stacks it is in.
- The `FillSpoilingFirst` setting now only matters when two foods make an equally good barrel. Then Brew now and Load pick the food closest to spoiling, or the freshest.
- Made for Survival Log 1.1.20719.

## 0.2.3

- **Warning:** Brewing Hub now needs Exiled's Toolkit 0.19.0 or newer. Update the Toolkit first.
- Made for Survival Log 1.1.20719.

## 0.2.2

- Fixed for the 1.1.20719 game update: a barrel can now start as soon as it holds enough satiety for one bottle, which is when the game lets you start it. With a bottle yield talent, that is less satiety than before. Brewing Hub also no longer blocks a brew the game would start.
- Made for Survival Log 1.1.20719.

## 0.2.1

- Brewing Hub's messages (what a button did, barrel notices) now all show as Exiled's Toolkit toasts. Those toasts also go to the Notifications history.
- Exiled's Toolkit 0.11.0 or newer is now required.
- Made for Survival Log 1.1.20151.

## 0.2.0

- New: `UiScale` under Interface lets you set a size for Brewing Hub's windows alone, from 75% to 200%, whatever the UI size in Exiled's Toolkit is. At 0, the default, they follow the Toolkit's. Needs Exiled's Toolkit 0.11.0 or newer.
- Made for Survival Log 1.1.19611.

## 0.1.8

- Less stutter the first time you open a brewing barrel after starting the game: Brewing Hub now gets ready in the background while the game loads.
- Made for Survival Log 1.1.19611.

## 0.1.7

- The Brewing Hub column on the brewing screen can be drawn bigger or smaller with the UI size setting of Exiled's Toolkit 0.10.0 or newer. A change shows at once, and the column only grows as far as the whole brewing screen still fits.
- Made for Survival Log 1.1.19611.

## 0.1.6

- Fixed: the search box on the brewing screen now accepts Chinese and other typing that needs an input method.
- Made for Survival Log 1.1.19611.

## 0.1.5

- The brewing screen no longer stutters while it is open, and loading a save or switching the language no longer freezes the game for a moment. The screen also stops redrawing when nothing on it has changed.

## 0.1.4

- Less stutter: while the brewing screen is closed, the mod only checks your barrels, instead of reading every food in your storages every few seconds.

## 0.1.3

- Fixed: Load could take food out of a stove that was cooking, which stopped that stove. Food in a stove is no longer used.

## 0.1.2

- Fixed: Load could put too much in the barrel when a stack couldn't be split. It now moves what fits and tells you how much went in.
- Collect and brew again no longer starts a barrel that Load couldn't fill.
- With PutBottlesAway on, the message counts the bottles that actually moved. The setting's description now says it moves every bottle of that drink in your bag.
- Fixed: keys stopped working if the brewing screen closed while you were typing in a search box.

## 0.1.1

- The brewing screen no longer jumps in height as food goes in and out of the barrel.

## 0.1.0

- First release.
- The brewing barrel screen gets a Brewing Hub column with Brew now, Recipes, Ingredients and Barrels tabs.
- As you fill a barrel, it shows the quality and which drink you'll get, with one-click changes for a better drink or one more bottle.
- Load fills the barrel from your bag and storages, and Empty to bag takes everything out.
- Collect and brew again, and a "Brew again when collected" switch, refill a finished barrel for the same drink.
