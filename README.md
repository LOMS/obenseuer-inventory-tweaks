# Inventory Tweaks

A BepInEx 6 mod for Obenseuer that adds quality-of-life features to the game's
inventory screens.

## Features

**Quick transfer between inventories** (e.g. inventory ↔ storage)
- **Move stack** (Shift + Left Click): move the whole stack with click
- **Move one** (Shift + Right Click): move a single item
- **Move all of a kind** (Shift + Ctrl + Left Click): move every stack of that
  item from the same inventory

**Storage window buttons**
- **Stack** (new button in storage inventory): top-up container with items from your inventory.
  Moves items of every type already in the container from your inventory into it.
- **Stack+** (Shift + Click on the Stack button): the same, also taking from your backpack
- **Stack allowed** (Alt + Click on the Stack button) - for containers that accept specific items, e.g. a bottle crate or ore wagon.
- **Stack allowed+** (Shift + Alt + Click on the Stack button) - Same, also takes from your backpack
- **Take similar** (Alt + Click on the 'Take all' button): take from the
  container only the item types you already have in your inventory

**Bulk item actions**
- **Break all** (Shift + Click on the Break button): break the whole stack
  (bottles, jars, glass panes, ...)
- **Slaughter all** (Shift + Click on the Slaughter button): slaughter every
  animal of the same kind in the same inventory, with a confirmation click

**Gardens and animal cages**
- **Plant max** (Shift + Click on a recipe in the list): plant as many as
  possible in one click

**No items/rats/cats on the floor**: results go to your inventory before falling on the ground.
Works for manual crafting, for breaking bottles inside bottle crates (does not accept glass),
for taking rats/cats out of breeding room etc.

## Details

- All moves and actions go through the game's own logic, so the usual rules
  still apply: stack limits, allowed categories, required tools, trading.
- Vanilla Shift behavior ("move one" on hold or double-click) is replaced by the
  shortcuts above. Ctrl ("half the stack") on hold or double-click is unchanged.
- With no storage open, Move stack equips the item, like a vanilla double-click.
- Move all of a kind works only with a storage open; in trade it moves just the
  clicked stack, so it never sells or buys everything at once.
- Move all of a kind, Stack and Take similar treat liquid containers by their
  contents: empty containers go with empty ones, filled ones only with the same
  container holding the same liquid.
- Stack and Take similar top up existing stacks first, then use free slots.
  Take similar ignores the backpack: it neither counts its items nor puts
  anything there.
- Slaughter all: the button turns into "Slaughter N?" for confirmation — click again within
  3 seconds to confirm. **Your own cat is never included**.
- Crafting results go to your inventory only while the station's window is
  open; background crafting keeps the vanilla behavior. Anything that fits
  nowhere still drops at the usual spot.

## Installation
Requires BepInEx 6 (Unity.Mono). Copy `InventoryTweaks.dll` to
`<Obenseuer>\BepInEx\plugins\InventoryTweaks\`.

## License
[MIT](LICENSE)
