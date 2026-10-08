# Inventory Tweaks

A BepInEx 6 mod for Obenseuer that adds quality-of-life features to the game's
inventory screens.

## Features

**Quick transfer between inventories** (e.g. inventory ↔ storage)
- **Move stack** (Shift + Left Click): move the whole stack
- **Move one** (Shift + Right Click): move a single item
- **Move all of a kind** (Shift + Ctrl + Left Click): move every stack of that
  item from the same inventory

**Storage window buttons**
- **Stack** (new button between the Sort and Take all buttons): move items of
  every type already in the container from your inventory into it
- **Stack+** (Shift + Click on the Stack button): the same, also taking from
  your backpack
- **Take similar** (Alt + Click on the Take all button): take from the
  container only the item types you already have in your inventory

**Bulk item actions**
- **Break all** (Shift + Click on the Break button): break the whole stack
  (bottles, jars, glass panes, ...)
- **Slaughter all** (Shift + Click on the Slaughter button): slaughter every
  animal of the same kind in the same inventory, with a confirmation click

**No items on the floor**: when a container is full, results go to your
inventory instead of falling on the ground — for item actions used inside a
container, crafting at a station and harvesting a garden or an animal cage.

## Details

- All moves and actions go through the game's own logic, so the usual rules
  still apply: stack limits, allowed categories, required tools, trading.
- Vanilla Shift behavior ("move one" on hold or double-click) is replaced by the
  shortcuts above. Ctrl ("half the stack") on hold or double-click is unchanged.
- With no storage open, Move stack equips the item, like a vanilla double-click.
- Move all of a kind works only with a storage open; in trade it moves just the
  clicked stack, so it never sells or buys everything at once.
- Stack and Take similar top up existing stacks first, then use free slots.
  Take similar ignores the backpack: it neither counts its items nor puts
  anything there.
- Slaughter all: the button turns into "Slaughter N?" — click again within
  3 seconds to confirm. Your own cat is never included. Stops when you run out
  of axes. Repeated action sounds are collapsed into one or two.
- Crafting results go to your inventory only while the station's window is
  open; background crafting keeps the vanilla behavior. Anything that fits
  nowhere still drops at the usual spot.

## Installation
Requires BepInEx 6 (Unity.Mono). Copy `InventoryTweaks.dll` to
`<Obenseuer>\BepInEx\plugins\InventoryTweaks\`.

## License
[MIT](LICENSE)
