# Inventory Tweaks

A BepInEx 6 mod for Obenseuer that adds quick item transfer between inventories
(e.g. backpack ↔ storage):

- **Shift + Left Click**: move the whole stack
- **Shift + Right Click**: move one item

Moves are instant and follow the game's usual rules (stack limits, allowed
categories, trading). With no storage open, Shift + Click equips the item, like
a vanilla double-click. Vanilla Shift behavior ("move one" on hold or
double-click) is replaced.

Item actions with Shift held (the button label changes to show it):

- **Shift + Break**: break the whole stack (bottles, jars, glass panes, ...)
- **Shift + Slaughter**: slaughter every animal of the same kind in the same
  inventory. The button turns into "Slaughter N?" — click again within 3 seconds
  to confirm. Your own cat is never included. Stops if you run out of axes.

## Installation
Requires BepInEx 6 (Unity.Mono). Copy `InventoryTweaks.dll` to
`<Obenseuer>\BepInEx\plugins\InventoryTweaks\`.
