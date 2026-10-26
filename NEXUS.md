Name: Brewing Hub
Summary: A better brewing barrel screen: the best barrel of each drink from your food, the quality and bottles as you fill, one-click changes for a better drink or one more bottle, and Load from your storages. Also lists every drink and what it is for, each food's numbers and every barrel in your base, with Collect and brew again. English and Chinese.

Description (BBCode):
Know what a barrel will brew before you start it. Brewing Hub adds a column to the game's brewing barrel screen with the best barrel of each drink you can make from your bag and home storages, the drinks and what they are for, every brewable food's numbers, and every barrel in your base. As you fill a barrel it shows the quality, the drink, the bottles and the brewing time, with one-click changes that make a better drink or one more bottle.

[size=5]Main features[/size]
[list]
[*][b]Brew now[/b]: the best barrel of Strong, Average and Poor drink your food can make, with the bottles, the quality, the brewing time and where each food comes from (food that spoils within a day is marked). Load puts what is in the barrel back in your bag, then fills it from your bag and storages. Load uses the food closest to spoiling first, or the freshest first if you prefer (setting [font=Courier New]FillSpoilingFirst[/font]). Spoiled food is never used.[/*]
[*][b]Quality as you fill[/b]: under the barrel, the mix's quality (total morale over total satiety), the line for the next drink up, the satiety that fills no bottle and how much more fills the next one.[/*]
[*][b]One-click changes[/b]: above the start button, buttons to add a food from your bag or storage, or take one out, when that makes a better drink or one more bottle, with what the barrel would make after.[/*]
[*][b]Recipes[/b]: each drink with its quality range, its description, the foods best for it, what it is used in and how many relief requests ask for it, plus your talents' effect on brewing time and satiety per bottle.[/*]
[*][b]Ingredients[/b]: every brewable food with its satiety, morale, quality, how many it takes for one bottle, how many you have, and how many more lift the barrel to the next drink. Search, filter and sort. Click a row to put one in the barrel.[/*]
[*][b]Barrels[/b]: every brewing barrel in your base, what it is brewing, the time left, and a Collect button on finished ones.[/*]
[*][b]Collect and brew again[/b]: on a finished barrel, one button takes the bottles and fills and starts it again for the same drink. A "Brew again when collected" switch does that on its own whenever the barrel's bottles are taken. The switch lasts until you leave the game.[/*]
[*][b]Put bottles away[/b] (setting [font=Courier New]PutBottlesAway[/font], off by default): when you collect a barrel, every bottle of that drink in your bag moves to a home storage that already holds it.[/*]
[/list]
With Exiled's Toolkit, a barrel finishing and a barrel brewing again show as toasts.

Brewing Hub adds nothing to your save and does not edit game files. You can remove it at any time.

Works in English and Chinese.

[center][img]https://sldb.core-directive.com/img/mods-banner.jpg[/img]
[size=4][url=https://www.nexusmods.com/survivallog/mods/73]Get Mod Manager[/url] to set up all your mods in game  |  [url=https://sldb.core-directive.com/en/]Open the Survival Log wiki[/url] for items, recipes and maps[/size][/center]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.19611. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/74]Stacking[/url]. Brewing Hub moves food Stacking made stackable one unit at a time, the way Stacking wants it. Without Stacking, nothing changes.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game, and barrel notices show as toasts. Without it, settings take effect at the next game start.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\BrewingHub.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]BrewingHub.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder.

[size=5]Configuration[/size]
To change which food Load uses first, or whether bottles are put away, edit [font=Courier New]BepInEx\config\com.dev.survivallog.brewinghub.cfg[/font] while the game is closed, or use the Mod Manager's Mods screen (see [url=https://github.com/AnExiledDev/SurvivalLog-BrewingHub/blob/main/CONFIG.md]CONFIG.md[/url] for every setting; the cfg file explains each setting in English and Chinese).

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or post in this page's Posts tab. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and, if you turned on [font=Courier New]LogToFile[/font], [font=Courier New]BepInEx\brewing-hub.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-BrewingHub]github.com/AnExiledDev/SurvivalLog-BrewingHub[/url]

[line]
[size=5]简体中文[/size]

开桶之前就知道能酿出什么。Brewing Hub 在游戏的酿酒桶界面加入一栏，显示用背包和家中储物能酿出的每种酒的最佳一桶、每种酒和它的用途、每种可酿食材的数值，以及基地里的所有酿酒桶。装桶时会显示品质、酿出的酒、瓶数和酿造时间，并有一键改进按钮，让酒更好或多出一瓶。

[size=5]主要功能[/size]
[list]
[*][b]现在能酿[/b]：用你的食材能酿出的烈性、普通和劣质酒的最佳一桶，显示瓶数、品质、酿造时间和每种食材的位置（一天内会变质的食材会标出）。「放入」会先把桶里的食材取回背包，再从背包和储物装桶。「放入」默认优先使用快变质的食材，也可改为优先使用最新鲜的（设置 [font=Courier New]FillSpoilingFirst[/font]）。变质的食材不会被使用。[/*]
[*][b]装桶时显示品质[/b]：酿酒桶下方显示这桶的品质（总心态除以总饱腹）、下一种酒的分界、装不满一瓶的饱腹，以及再加多少可以装满下一瓶。[/*]
[*][b]一键改进[/b]：开始按钮上方的按钮可以从背包或储物加入一种食材，或取出一份，只在能让酒更好或多出一瓶时显示，并标出改完后能酿出什么。[/*]
[*][b]酒谱[/b]：每种酒的品质范围、说明、最适合的食材、可用于什么，以及有多少个救济委托需要它，并列出天赋对酿造时间和每瓶所需饱腹的影响。[/*]
[*][b]食材[/b]：每种可酿食材的饱腹、心态、品质、一瓶需要几份、你有多少，以及再加几份能让这桶升到下一种酒。可搜索、筛选和排序。点击一行可放一份进桶。[/*]
[*][b]酿酒桶[/b]：基地里的所有酿酒桶、正在酿什么、还剩多久，酿好的桶有「取出」按钮。[/*]
[*][b]取出并再酿一桶[/b]：酿好的桶上，一个按钮就能取出酒瓶，并为同一种酒重新装桶、开始酿造。打开「取出后再酿一桶」开关后，无论怎样取出这个桶的酒瓶，都会自动这样做。离开游戏后开关关闭。[/*]
[*][b]自动收酒[/b]（设置 [font=Courier New]PutBottlesAway[/font]，默认关闭）：取出一桶酒时，背包里所有这种酒都会移到已经放有这种酒的家中储物里。[/*]
[/list]
安装 Exiled's Toolkit 后，酿好的桶和再酿一桶都会弹出提示。

Brewing Hub 不会向存档添加任何内容，也不会修改游戏文件，可以随时移除。

支持英文和中文。

[url=https://www.nexusmods.com/survivallog/mods/73]获取 Mod Manager[/url]，在游戏里设置所有模组。[url=https://sldb.core-directive.com/zh/]打开 Survival Log 维基[/url]，物品、配方、地图一查就有。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.19611。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/74]Stacking[/url]。Brewing Hub 会按 Stacking 的规则，把它变成可堆叠的食材一份一份放入桶中。未安装 Stacking 时不受影响。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 或更新版本。安装后，在 Mod Manager 的模组界面修改的设置无需重启游戏即可生效，酿酒桶的通知也会弹出提示；未安装时，设置在下次启动游戏时生效。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\BrewingHub.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]BrewingHub.dll[/font]。

[size=5]配置[/size]
如需修改放入食材的顺序或是否自动收酒，请在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.brewinghub.cfg[/font]，或使用 Mod Manager 的模组界面。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-BrewingHub/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font]，如果打开了 [font=Courier New]LogToFile[/font]，也请附上 [font=Courier New]BepInEx\brewing-hub.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-BrewingHub]github.com/AnExiledDev/SurvivalLog-BrewingHub[/url]
