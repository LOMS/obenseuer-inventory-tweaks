# Inventory Tweaks

A BepInEx 6 mod for Obenseuer that adds quick item transfer between inventories
(e.g. backpack ↔ storage):

- **Shift + Left Click**: move the whole stack
- **Shift + Right Click**: move one item
- **Shift + Ctrl + Left Click**: move every stack of that item from the same
  inventory (only when a storage is open; in trade it works like Shift + Click)

Moves are instant and follow the game's usual rules (stack limits, allowed
categories, trading). With no storage open, Shift + Click equips the item, like
a vanilla double-click. Vanilla Shift behavior ("move one" on hold or
double-click) is replaced.

**Stack** button in the storage window (between Sort and Take all): moves
items of every type already in the container from your inventory into it,
topping up existing stacks first, then free slots. **Shift + Click** ("Stack+")
also takes them from your backpack.

Item actions with Shift held (the button label changes to show it):

- **Shift + Break**: break the whole stack (bottles, jars, glass panes, ...)
- **Shift + Slaughter**: slaughter every animal of the same kind in the same
  inventory. The button turns into "Slaughter N?" — click again within 3 seconds
  to confirm. Your own cat is never included. Stops if you run out of axes.

Results that don't fit into a container go to your inventory instead of
falling on the ground:

- item actions (Break, Slaughter, ...) used on an item inside a container;
- crafting at a station while you have its window open;
- harvesting a garden or an animal cage from its window.

## License
[MIT](LICENSE)

## Installation
Requires BepInEx 6 (Unity.Mono). Copy `InventoryTweaks.dll` to
`<Obenseuer>\BepInEx\plugins\InventoryTweaks\`.
