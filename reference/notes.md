# Research notes

File paths are relative to `reference\decompiled\`. Only summaries and names
here; game code is not copied.

## Environment (verified)
- `BepInEx\core`: BepInEx.Core / BepInEx.Unity.Mono / BepInEx.Unity.Common
  6.0.0-be.788, 0Harmony 2.10.2 (HarmonyX). Built for .NET 3.5 (mscorlib 2.0).
- `Obenseuer_Data\Managed`: Assembly-CSharp references mscorlib 4.0 and
  netstandard 2.1; UnityEngine.InputLegacyModule and Unity.InputSystem are present.

Sections 1–7 describe the game; "Mod design" at the end describes what the mod does.

## 1. Slot and item, mouse input
- `Slot.cs` — `Slot : MonoBehaviour` + `IPointerDownHandler`, `IPointerClickHandler`,
  `IDropHandler`, enter/exit. This is a grid cell.
  - `slotcontrol` (`SlotController`) — cell data; `playerSlot` — whether the cell
    belongs to the player or to a foreign container.
  - `OnPointerDown` (line ~636) just forwards the event to `ItemData.OnPointerDown`
    of the item in the cell. `OnPointerClick` only selects the slot.
- `ItemData.cs` — UI object of the item inside a slot (prefab
  `Inventory.instance.inventoryPanelUI.inventoryItem`, created in
  `Slot.SetSlotController` / `Slot.UpdateSlot`).
  - Implements begin/drag/end drag, pointer down/enter/exit/click.
  - `static currentHoverItemData` — set in `OnPointerEnter`, cleared in
    `Deactivate()` (`OnPointerExit`, `OnDestroy`).
  - `static globaldrag` — the item currently being dragged.

## 2. Hold-to-move
- `HoldController.cs` (singleton `HoldController.instance`):
  - `holdTimeLength` — `[SerializeField]`, defaults to `1f` (actual value in the
    scene not checked).
  - `Update()`: every frame calls `currentHoverItemData.HoldMove(false, 0f, ...)`
    (this **resets** the indicator fill to 0), then `UpdateHold` for Mouse0 and
    Mouse1.
  - `UpdateHold`: while the button is held, accumulates `Time.deltaTime`; after
    20 % of the threshold passes progress via `HoldMove(false, t/holdTimeLength)`;
    when the threshold is reached calls `HoldMove(true, ...)` (move) and, if
    `resetHold`, resets the timer (i.e. holding repeats the move every second).
  - `Holding()` — used in `ItemData.CantDrag()` so that holding does not turn
    into dragging.
- `ItemData.HoldMove(bool move, float value, out bool resetHold)` (line ~748):
  - picks the amount via `GetMoveMode()` (Full / Half / One);
  - `move == true` → `QuickMove(num)`; otherwise writes `value` into
    `progressIndicatorImage.fillAmount`.
- Double-click (`ItemData.OnPointerDown`, line ~568 → `DoubleClickController.DoubleClick`,
  0.5 s window) also calls `QuickMove`.
- **The method that actually moves:** `ItemData.QuickMove(int amount)` (line ~518):
  1. refuses if `itemDisabled`, `itemMoveDisabled`, `isDragging`, `globaldrag`,
     `!canMove`;
  2. special modes: `BottleRecyclingPanel.instance.open`,
     `LiquidStorage.currentStorage`, `TradePanel.instance.activeTrade`
     (trade → `Trade.TradeAndMove`);
  3. otherwise `slotController.MoveToOtherInventory(out moved, amount)`;
  4. **if `Inventory.instance.ForeignSlots == null` (no storage open), invokes the
     item's `ItemEquipable` actions** (i.e. a double-click with no storage open
     equips the item).
- **Target selection:** `SlotController.MoveToOtherInventory(out, amount)` (line ~639):
  - `slot.playerSlot` → `MoveToForeignInventory` → `Inventory.instance.ForeignSlots[0]`
    (if `ForeignSlots == null`, returns false and does nothing);
  - otherwise → `MoveToInventory` (target `null` = player inventory).
  - Inside: `ItemOperations.AddItemAndReturnRemaining(...)`, then shrinks the
    source stack; if nothing was moved — `InventoryFull()` / `StorageFull()`.
  - `ForeignSlots` is set by `Storage` on open (`Storage.cs` ~706/749),
    cleared on close (~792); also by `LiquidStorage`, `GrowingPanel`.

## 3. Input / Shift
- The game reads the **legacy `UnityEngine.Input`** (`Input.GetKey(KeyCode...)`,
  `GetMouseButtonDown`). The new Input System is used only in
  `ComputerControllerInput.cs` (the in-game computer).
- `InputManager.cs` — wrapper over key bindings (`GetKey("Walk")`, etc.) +
  `inputType` (Keyboard / Controller). Not used for mouse inventory input.
- **Vanilla Shift in the inventory:** `ItemData.GetMoveMode()` (line ~610):
  - `LeftControl` → Half (half the stack);
  - `LeftShift` **or** right mouse button → One (single item);
  - otherwise Full.
  So in vanilla, Shift+hold and Shift+double-click move **one item at a time**.
  Only `LeftShift` is checked (not `RightShift`).
- Gamepad/keyboard: `InventoryNavigationHandler` (~1017) — the `"Move Item"` key (F)
  while storage is open.

## 4. Existing indicator (not used by the mod; kept for reference)
- `ItemData.progressIndicatorImage` (`[SerializeField] Image`) — hold progress fill
  (`fillAmount`), driven only by `HoldMove` and reset in `Deactivate()`.
- `ItemData.progressIndicatorImage2` + `SetProgressIndicator2Value(float)` —
  a second indicator; **no callers found in game code** (looks unused).
- Fill type (Radial360 or other), sprite and color live in the prefab and are
  not visible from code (not checked; the indicator was dropped from scope).
- Note: `HoldController.Update` writes 0 into `progressIndicatorImage` of the
  hovered item every frame, so any custom value must be set **after** it
  (Postfix on `HoldController.Update` or `LateUpdate`).

## 5. Transfer checks (must not be bypassed)
All run inside `QuickMove` → `MoveToOtherInventory` →
`ItemOperations.AddItemAndReturnRemaining` → `SlotController.AddItemAndReturnRemaining`:
- `itemDisabled` / `itemMoveDisabled` flags, active drag;
- trade (`Trade.TradeAndMove`, money/prices), bottle recycling, liquids;
- `SlotController.OnDroppedCheck` (custom container checks);
- `CheckIfAllowed` — allowed/forbidden categories (by default `Storage` and
  `Unstorable` cannot be stored);
- `ItemStack.AddItemToPanel` — stack size, liquid/quality merging;
- `Siblings` — overflow into neighboring cells;
- for the player inventory: character slots, then the backpack
  (`BackpackStorage.instance.GetStorage()`); money (ID 0/1) → `Money.AddMoney`;
  theft (`CheckForAddedStolenItems`, `Crime`).
- **No weight system** found on these paths.
- Conclusion: reuse `ItemData.QuickMove(amount)` so all checks are preserved
  automatically.

## 6. Item actions (Break / Slaughter)
- Item definitions are data-driven: `<GameDir>\Obenseuer_Data\StreamingAssets\Items.json`
  (loaded by `ItemDatabase`, FullSerializer). Read-only for us.
- `Item.ItemAction` (`Item.cs`) — base class: `Name` (button text, not localized),
  `UseSound`, `virtual Invoke(ItemData, bool worlduse)`, `virtual CanUse(...)`.
  `Item.Actions[]` — the item's actions; `Item.InvokeAction(itemData, action)`
  redirects to trade if a trade is active, otherwise calls `action.Invoke`.
- Both Break and Slaughter are `OS.Items.ItemConsumable` (`OS.Items\ItemConsumable.cs`):
  - `Invoke`: base Invoke (sound, `OnItemConsumed`, special triggers, theft check),
    `PlayerStats.UpdateValues`, then if `DestroyAfterUse` removes **one** item via
    `ItemInfo.instance.RemoveItem(itemData)` → `slotController.TakeItem()`;
    consumes `RequiredItems` via `Inventory.FindAndChangeItemAmmo(id, -1)` (tool
    durability); spawns `ItemsToSpawn` into the same `SlotController`
    (`ItemOperations.AddItemWithPermission`).
  - `CanUse`: false if the player lacks `RequiredItems`.
- Break items (Type `Container` / `None` / `PiggyBank`, stack 8 unless noted):
  Empty Wine Bottle, Empty Glass Bottle, Empty Clear Glass Bottle, Small Glass
  Bottle, Fancy Glass Bottle, Glass Flask, Mason Jar, Glass Pane (→ 3 shards),
  Piggy Bank (stack 1), Cash Box (stack 1). Spawns Glass shards (ID 35030).
- Slaughter items (Type `Slaughter`, all stack 1): Rat, Glowing rat, Chick (no
  tool); Cat, Glowing Cat, Your Cat, Chicken, Pig (require "Axes" group, ID 19720).
  Cats have two actions: `Pet` (index 0) and `Slaughter` (index 1).
  **"Your Cat" (ID 15650) is the player's pet.**
- UI: `ItemInfo` (singleton) + `ItemInfoPanel`. Only **two** action buttons:
  `UseItem()` → `UseItem(0, false)`, `UseItemSecondary()` → `UseItem(1, true)`.
  `ItemInfo.UseItem(int index, bool secondaryButton)` has a ~0.33 s cooldown
  (skipped during trade), handles liquid children, then invokes `Actions[index]`
  and refreshes the panel.
- Action sound: base `ItemAction.Invoke` plays `UseSound` via
  `ItemInfo.PlaySound(AudioClip)` (`PlayOneShot`) — once per unit; no other
  per-unit sounds on the Break/Slaughter path.
- Confirmation UI: `ConfirmationNotification` (`ShowNotification(title, content,
  confirmText, Action)`) exists but is only referenced by `ShopBaseUI.notification`
  (lives inside the shop UI). No generic dialog found.

## 7. Where produced items go (drop path)
- `ItemConsumable.Invoke` spawns results via `ItemOperations.AddItemWithPermission`
  → `ItemOperations.AddItems(item, amount, owner, meta, slot, notify, ignoreCrime)`
  → `slot.AddItem` → `SlotController.AddItemAndReturnRemaining(dropItems: true)`
  (target slot, then `Siblings`, then `ItemOperations.DropItem` on the ground).
- Crafting: `CraftingBase.CreateItems(...)` (protected virtual, overridden by
  `manufacturingProcess`) → `Storage.AddItem` (OutputStorage ?? InputOutputStorage)
  → `Slots[0].AddItem(..., dropper: station)` → same `AddItemAndReturnRemaining(dropItems: true)`.
  `CraftingBase.currentCraftingBase` / `manuActive` = the player has the station UI open
  (set in `Enter()`, cleared in `Exit()`).
- Harvest: gardens and animal cages are both `Growing : CraftingBase` (animals are
  "plants"; `AnimalBreeder` only breeds). UI harvest = `Growing.Harvest()`
  (`GrowingPanel.PressHarvestButton`) and `Growing.Harvest(GameObject)`
  (`ProgressSlot` click) → private `Growing.CreateItems` → `Storage.AddItemToStorage` /
  `Storage.AddItem` → `Slots[0].AddItem(..., overflowSpot)` → same drop path.
  Harvest by interacting with a plant in the world (`grow.Interact` → `grow.Harvest`)
  already adds straight to the player via `ItemOperations.AddItems(stack)`.
# Mod design (decisions agreed with the user)

## A. Quick transfer — `src\Patches\ShiftClickPatches.cs`
- Prefix on `ItemData.OnPointerDown`, instant, via `QuickMove`:
  - Shift+LMB — whole stack; Shift+RMB — one item.
  - Shift+Ctrl+LMB — every stack with the same `itemId` in `slotController.Siblings`
    (each via its UI `ItemData.QuickMove`); stops at the first failed move (target
    full) to avoid repeated "full" messages; one sound per click. Only for plain
    storage transfer (`ForeignSlots != null`, no trade, no liquid storage, no bottle
    recycling); otherwise acts as Shift+LMB. Shift+Ctrl+RMB = Shift+RMB.
- With no storage open — same as vanilla (`QuickMove` equips the item).
- Left Shift only. No progress indicator.
- Vanilla Shift disabled: Transpiler on `ItemData.GetMoveMode` replaces
  `Input.GetKey` with a version that ignores LeftShift. Vanilla Ctrl ("half") on
  hold / double-click is unchanged.

## B. Bulk item actions — `src\Patches\BulkActionPatches.cs`
- Prefix on `ItemInfo.UseItem(int, bool)` (vanilla guards mirrored: buttons
  enabled, no cooldown, no trade, no liquid child).
- Shift+Break — repeats the action over the whole stack, for every stackable
  item with a "Break" action.
- Shift+Slaughter — all animals of the **same item ID** in the same container
  (`SlotController.Siblings`); "Your Cat" (15650) is always excluded; stops when
  `CanUse` fails (no axe). Confirmation: first click turns the button into
  "Slaughter N?", a second click within 3 s confirms. One animal → plain vanilla.
- Labels: while Shift is held the button shows "Break all" / "Slaughter all";
  set via `ItemInfoPanel.buttonText` / `secondaryButtonText` (private,
  FieldRefAccess) from `Plugin.LateUpdate`, restored when Shift is released.
- Sounds: Prefix on `ItemInfo.PlaySound` during a bulk action lets the first
  sound through and suppresses the rest; one more plays after `SecondSoundDelay`
  (realtime) if anything was suppressed.

## C. Overflow to player — `src\Patches\OverflowToPlayerPatches.cs`
- Scopes (counter, closed in a Finalizer): `ItemConsumable.Invoke` on an item in
  a non-player slot; `CraftingBase.CreateItems` while `currentCraftingBase == this`
  (background crafting keeps vanilla); both `Growing.Harvest` overloads.
- Inside a scope, `SlotController.AddItemAndReturnRemaining(dropItems: true)` on a
  container slot adds without dropping, sends the remainder to
  `ItemOperations.AddItemAndReturnRemaining(slot: null)` (player main slots,
  character slots, backpack, stolen-item checks), then drops only what is left
  (same dropper/position).

## Common limitations
- Bulk operations (B, Shift+Ctrl in A) need the item's UI `ItemData`; slots that
  are not displayed are skipped.
