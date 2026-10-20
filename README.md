# Inventory Tweaks

A BepInEx 6 mod for Obenseuer that adds quality-of-life features to the game's
inventory screens.

## Features

**Quick transfer between inventories** (e.g. inventory ↔ storage)
- **Move stack** (Shift + Left Click): move the whole stack with click
- **Move one** (Shift + Right Click): move a single item
- **Move all of a kind** (Shift + Ctrl + Left Click): move every stack of that
  item from the same inventory

<details>
<summary>▶ Video: Move stack / Move one</summary>

https://github.com/user-attachments/assets/96306874-324a-4046-9d58-edcfa4c91638

</details>

<details>
<summary>▶ Video: Move all of a kind</summary>

https://github.com/user-attachments/assets/f67e329b-05de-4c70-b9ae-9c3ecf2dbf76

</details>

**Storage window buttons**
- **Stack** (new button in storage inventory): top-up container with items from your inventory.
  Moves items of every type already in the container from your inventory into it.
- **Stack+** (Shift + Click on the Stack button): the same, also taking from your backpack
- **Stack allowed** (Alt + Click on the Stack button) - for containers that accept specific items, e.g. a bottle crate or ore wagon.
- **Stack allowed+** (Shift + Alt + Click on the Stack button) - Same, also takes from your backpack
- **Take similar** (Alt + Click on the 'Take all' button): take from the
  container only the item types you already have in your inventory

<details>
<summary>▶ Video: Stack</summary>

https://github.com/user-attachments/assets/a2d8d6f8-79ff-4e3f-b0de-e029260ea46b

</details>

<details>
<summary>▶ Video: Stack allowed</summary>

https://github.com/user-attachments/assets/8a319c53-8055-4092-ab5e-70a8c865f0d2

</details>

**Bulk item actions**
- **Break all** (Shift + Click on the Break button): break the whole stack
  (bottles, jars, glass panes, ...)
- **Slaughter all** (Shift + Click on the Slaughter button): slaughter every
  animal of the same kind in the same inventory, with a confirmation click

<details>
<summary>▶ Video: Break all / Slaughter all</summary>

https://github.com/user-attachments/assets/c81f8f15-3eaa-4375-bca6-fb428a1e9a01

</details>

**Crafting stations** (the Craft button)
- **Craft one** (Shift + Click on a recipe in the list): craft it once, without opening the recipe
  and pressing Craft. Works only if the recipe can be crafted now; otherwise the click is a normal one
- **Craft amount** (Alt + Click on the Craft button): choose how many times to craft
- **Craft all** (Ctrl + Click on the Craft button): craft until the ingredients run out
- **Esc** stops the **Craft amount** and **Craft all** sequences; the station window stays open

<details>
<summary>▶ Video: craft amount/all demonstration</summary>

https://github.com/user-attachments/assets/972c2720-6dbc-4eb9-a3fe-44f5ee6a04f1

</details>


**Gardens and animal cages**
- **Plant max** (Shift + Click on a recipe in the list): plant as many as
  possible in one click
- **Plant from the inventory**: your main inventory counts as part of the
  garden / cage storage. Seeds (animals) in your inventory are enough to see the
  recipe, plant with the Plant button or Plant max — the missing items are moved
  into the garden automatically right before planting, worst quality first

<details>
<summary>▶ Video: Plant max</summary>

https://github.com/user-attachments/assets/972c2720-6dbc-4eb9-a3fe-44f5ee6a04f1

</details>

**No items on the floor**: results of item actions inside a container go to your inventory
before falling on the ground. Works for breaking bottles inside bottle crates (they do not accept glass), etc.

## Details

- Buttons whose behavior the mod changes (or adds) have a small hammer in the top-right corner.
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
- Anything that fits nowhere still drops at the usual spot.
- A station that is switched off (or out of fuel) is switched on automatically by any click
  on Craft, including a normal one. When its crafting is over, a station switched on this way
  is switched off again (a station you had switched on yourself stays on).
- Craft amount / Craft all press Craft for you, one craft at a time, waiting until the
  button is available again.
  The run ends by itself when the ingredients run out, the station window is closed,
  another recipe is selected, or the station cannot be powered (no fuel, no electricity).
  A normal click on Craft during a run hands control back to you.

## Installation
Requires BepInEx 6 (Unity.Mono). Download the zip from
[Releases](https://github.com/LOMS/obenseuer-inventory-tweaks/releases) and extract
it into `<Obenseuer>\BepInEx\` (the DLL ends up in
`BepInEx\plugins\InventoryTweaks\InventoryTweaks.dll`).

## License
[MIT](LICENSE)
