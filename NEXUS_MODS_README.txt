[size=4][b]Description[/b][/size]
[color=#D4D4D8]TLDR: This mod simply cuts down on the number of clicks needed to manage your inventory and crafting. I made some videos for you  - check them out!

All modded features are documented in game. Whenever you see small hammer icon in the button - read the tooltip, it will tell you everything.
[img]https://staticdelivery.nexusmods.com/mods/6792/images/25/25-1792409345-614590702.jpg[/img]

LR:
Inventory Tweaks adds keyboard shortcuts and a few extra buttons to Obenseuer's inventory, storage, crafting and garden screens.
Everything goes through the game's own logic, so the usual rules still apply: stack limits, allowed categories, required tools, trading.
Changed buttons have a small hammer in the top-right corner and a tooltip with their shortcuts.
Source code: [url=https://github.com/LOMS/obenseuer-inventory-tweaks]https://github.com/LOMS/obenseuer-inventory-tweaks[/url]

[size=4][b]Features:[/b][/size][/color]

[color=#D4D4D8][b]Quick transfer between inventories:[/b][/color]
[list]
[*][color=#bf9000][b][Shift + Left Click][/b][/color][color=#D4D4D8] - move the whole stack[/color][/*]
[*][color=#bf9000][b][Shift + Right Click][/b][/color][color=#D4D4D8] - move a single item[/color][/*]
[*][color=#bf9000][b][Shift + Ctrl + Left Click][/b][/color][color=#D4D4D8] - move every stack of that item[/color][/*]
[/list]
[color=#D4D4D8][b]Storage window buttons:[/b][/color]
[list]
[*][color=#D4D4D8]Stack (new button) - moves items of the types already in the container from your inventory into it. [/color][color=#bf9000][b][Shift + Click][/b][/color][color=#D4D4D8] - also from your backpack.[/color][/*]
[*][color=#bf9000][b][Alt + Click][/b][/color][color=#D4D4D8] on Stack ("Stack allowed") - for containers that accept only specific items (bottle crate, ore wagon): moves everything the container accepts. [/color][color=#bf9000][b][Shift + Alt + Click][/b][/color][color=#D4D4D8] - also from your backpack.[/color][/*]
[*][color=#bf9000][b][Alt + Click][/b][/color][color=#D4D4D8] on Take all ("Take similar") - takes only the item types you already have in your inventory.[/color][/*]
[/list]
[color=#D4D4D8][b]Bulk item actions:[/b][/color]
[list]
[*][color=#bf9000][b][Shift + Click][/b][/color][color=#D4D4D8] on Break - breaks the whole stack (bottles, jars, glass panes, ...).[/color][/*]
[*][color=#bf9000][b][Shift + Click][/b][/color][color=#D4D4D8] on Slaughter - slaughters every animal of the same kind in the container (click again to confirm). Your own cat is never included.[/color][/*]
[/list]
[color=#D4D4D8][b]Crafting stations:[/b][/color]
[list]
[*][color=#bf9000][b][Shift + Click][/b][/color][color=#D4D4D8] on a recipe - craft it once, without opening it and pressing Craft.[/color][/*]
[*][color=#bf9000][b][Alt + Click][/b][/color][color=#D4D4D8] on Craft - craft N times (you choose N).[/color][/*]
[*][color=#bf9000][b][Ctrl + Click][/b][/color][color=#D4D4D8] on Craft - craft until the ingredients run out.[/color][/*]
[*][color=#bf9000][b][Esc][/b][/color][color=#D4D4D8] - stops the sequence; the station window stays open.[/color][/*]
[*][color=#D4D4D8]A switched-off station is turned on automatically by any click on Craft, and turned off again when its crafting is over.[/color][/*]
[/list]
[color=#D4D4D8][b]Gardens and animal cages[/b]:[/color]
[list]
[*][color=#bf9000][b][Shift + Click][/b][/color][color=#D4D4D8] on a recipe - plant as many as possible in one click. Seeds (animals) are taken from your inventory and backpack, worst quality first.[/color][/*]
[*][color=#bf9000][b][Shift + Click][/b][/color][color=#D4D4D8] on a plant in the progress grid - harvest every ripe plant from the clicked one to the end of the grid (it and everything to its right and below) straight into your inventory. Ripe means past the growing stage: spoiling, spoiled, or fruiting with fruits ready; plants that are still growing are never touched. Stops when your inventory is full. These slots have no hammer - they keep the game's own tooltip.[/color][/*]
[/list]
[color=#D4D4D8][b]No items on the floor:[/b][/color]
[list]
[*][color=#D4D4D8]Results of item actions (Break, Slaughter, ...) inside a container go to your inventory when the container can't take them, e.g. broken glass from bottles in a bottle crate. Only what fits nowhere is dropped.[/color][/*]
[/list]
[color=#D4D4D8]Liquid containers are matched by their contents: empty bottles go with empty ones, filled ones only with the same liquid.[/color]

[size=4][b]Requirements[/b][/size]
[list]
[*]BepInEx 6 for Unity Mono, x64.
Download the "BepInEx Unity (Mono) for Windows (x64)" build from [url=https://builds.bepinex.dev/projects/bepinex_be]https://builds.bepinex.dev/projects/bepinex_be[/url], extract it into the game folder next to
[code]Obenseuer.exe[/code]
, and start the game once so BepInEx creates its folders.[/*]
[*]No other mods are needed.[/*]
[/list]

[size=4][b]Installation[/b][/size]
[list=1]
[*]Install BepInEx 6 (see Requirements) if you haven't yet.[/*]
[*]Download the mod archive.[/*]
[*]Extract it into
[code]<Obenseuer>\BepInEx\[/code]
The DLL should end up in
[code]BepInEx\plugins\InventoryTweaks\InventoryTweaks.dll[/code]
[/*]
[*]Start the game.[/*]
[/list]

[size=4][b]Deinstallation[/b][/size]
Delete the folder:
[code]BepInEx\plugins\InventoryTweaks[/code]
Nothing else to clean up: the mod stores no data of its own, so your saves are not affected.